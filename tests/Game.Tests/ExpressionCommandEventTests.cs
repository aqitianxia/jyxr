using Game.Application;
using Game.Core.Abstractions;
using Game.Core.Definitions;
using Game.Core.Model;

namespace Game.Tests;

public sealed class ExpressionCommandEventTests
{
    [Fact]
    public async Task CurrencyAndAdventureCommandsPublishEvents()
    {
        var session = new GameSession(new GameState(), TestContentFactory.CreateRepository());
        var currency = 0;
        var adventure = 0;
        using var a = session.Events.Subscribe<CurrencyChangedEvent>(_ => currency++);
        using var b = session.Events.Subscribe<AdventureStateChangedEvent>(_ => adventure++);

        await session.StoryService.CommandDispatcher.ExecuteCommandAsync("change_silver", [ExpressionValue.FromNumber(20)]);
        await session.StoryService.CommandDispatcher.ExecuteCommandAsync("change_morality", [ExpressionValue.FromNumber(5)]);

        Assert.Equal(1, currency);
        Assert.Equal(1, adventure);
    }

    [Fact]
    public async Task VariableCommandsPublishStoryStateEvents()
    {
        var session = new GameSession(new GameState(), TestContentFactory.CreateRepository());
        var count = 0;
        using var subscription = session.Events.Subscribe<StoryStateChangedEvent>(_ => count++);
        var dispatcher = session.StoryService.CommandDispatcher;

        await dispatcher.ExecuteCommandAsync("set_var", [ExpressionValue.FromString("counter"), ExpressionValue.FromNumber(1)]);
        await dispatcher.ExecuteCommandAsync("change_var", [ExpressionValue.FromString("counter"), ExpressionValue.FromNumber(2)]);
        await dispatcher.ExecuteCommandAsync("remove_var", [ExpressionValue.FromString("counter")]);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task FlagSugarSafelyProbesStrictBooleanStoryVariables()
    {
        var session = new GameSession(new GameState(), TestContentFactory.CreateRepository());
        var dispatcher = session.StoryService.CommandDispatcher;
        var evaluator = new ExpressionEvaluator();
        var parser = new ExpressionParser();
        var environment = new GameExpressionEnvironment(session).Create();

        Assert.False(evaluator.Evaluate(parser.ParseExpression("has_flag('met_heroine')"), environment).AsBoolean("test"));

        await dispatcher.ExecuteCallAsync(parser.ParseCall("set_flag('met_heroine')"));

        Assert.True(evaluator.Evaluate(parser.ParseExpression("has_flag('met_heroine')"), environment).AsBoolean("test"));
        Assert.True(session.State.Story.TryGetVariable("met_heroine", out var flag));
        Assert.True(flag.AsBoolean("test"));

        await dispatcher.ExecuteCallAsync(parser.ParseCall("clear_flag('met_heroine')"));
        Assert.False(evaluator.Evaluate(parser.ParseExpression("has_flag('met_heroine')"), environment).AsBoolean("test"));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await dispatcher.ExecuteCallAsync(parser.ParseCall("clear_flag('met_heroine')")));

        session.State.Story.SetVariable("counter", ExpressionValue.FromNumber(1));
        Assert.Throws<ExpressionEvaluationException>(() =>
            evaluator.Evaluate(parser.ParseExpression("has_flag('counter')"), environment));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await dispatcher.ExecuteCallAsync(parser.ParseCall("set_flag('counter')")));
    }

    [Fact]
    public async Task ChangeItemUsesSignedDeltaAndRemoveItemRequiresPositiveQuantity()
    {
        var item = new NormalItemDefinition { Id = "pill", Name = "pill", Type = ItemType.Utility, ConsumeOnUse = false };
        var session = new GameSession(new GameState(), TestContentFactory.CreateRepository(items: [item]));
        await session.StoryService.CommandDispatcher.ExecuteCommandAsync("change_item", [ExpressionValue.FromString("pill"), ExpressionValue.FromNumber(3)]);
        await session.StoryService.CommandDispatcher.ExecuteCommandAsync("item", [ExpressionValue.FromString("pill"), ExpressionValue.FromNumber(-1)]);
        Assert.True(session.State.Inventory.ContainsStack(item, 2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await session.StoryService.CommandDispatcher.ExecuteCommandAsync("remove_item", [ExpressionValue.FromString("pill"), ExpressionValue.FromNumber(-1)]));
    }

    [Theory]
    [InlineData("join_random")]
    public async Task RandomJoinUsesInjectedRandomSource(string commandName)
    {
        var first = TestContentFactory.CreateCharacterDefinition("first");
        var second = TestContentFactory.CreateCharacterDefinition("second");
        var random = new SelectingRandom(1);
        var session = new GameSession(
            new GameState(),
            TestContentFactory.CreateRepository(characters: [first, second]),
            randomService: random);
        var call = new ExpressionParser().ParseCall($"{commandName}(['first', 'second'])", "random join test");

        await session.StoryService.CommandDispatcher.ExecuteCallAsync(call);

        Assert.True(session.State.Party.ContainsMember("second"));
        Assert.Equal((0, 2), random.LastRange);
    }

    [Fact]
    public async Task RandomJoinValidatesEveryCandidateBeforeConsumingRandomness()
    {
        var first = TestContentFactory.CreateCharacterDefinition("first");
        var random = new SelectingRandom(0);
        var session = new GameSession(
            new GameState(),
            TestContentFactory.CreateRepository(characters: [first]),
            randomService: random);
        var call = new ExpressionParser().ParseCall("join_random(['first', 'missing'])", "random join test");

        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await session.StoryService.CommandDispatcher.ExecuteCallAsync(call));

        Assert.Null(random.LastRange);
        Assert.Empty(session.State.Party.Members);
    }

    [Fact]
    public async Task RandomJoinRejectsEmptyCandidateList()
    {
        var session = new GameSession(new GameState(), TestContentFactory.CreateRepository());
        var call = new ExpressionParser().ParseCall("join_random([])", "random join test");

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await session.StoryService.CommandDispatcher.ExecuteCallAsync(call));
    }

    [Theory]
    [InlineData("join", false)]
    [InlineData("follow", true)]
    public async Task PartyEntryCommandsPreserveOptionalDefinitionId(string commandName, bool follower)
    {
        var identityDefinition = TestContentFactory.CreateCharacterDefinition("identity");
        var templateDefinition = TestContentFactory.CreateCharacterDefinition("template");
        var session = new GameSession(
            new GameState(),
            TestContentFactory.CreateRepository(characters: [identityDefinition, templateDefinition]));

        await session.StoryService.CommandDispatcher.ExecuteCallAsync(
            new ExpressionParser().ParseCall($"{commandName}('identity', 'template')", "party entry test"));

        var character = follower
            ? session.State.Party.Followers.Single(candidate => candidate.Id == "identity")
            : session.State.Party.GetMember("identity");
        Assert.Equal("template", character.Definition.Id);
    }

    [Theory]
    [InlineData("join", false)]
    [InlineData("follow", true)]
    public async Task PartyEntryCommandsDefaultDefinitionIdToCharacterId(string commandName, bool follower)
    {
        var definition = TestContentFactory.CreateCharacterDefinition("identity");
        var session = new GameSession(
            new GameState(),
            TestContentFactory.CreateRepository(characters: [definition]));

        await session.StoryService.CommandDispatcher.ExecuteCallAsync(
            new ExpressionParser().ParseCall($"{commandName}('identity')", "party entry test"));

        var character = follower
            ? session.State.Party.Followers.Single(candidate => candidate.Id == "identity")
            : session.State.Party.GetMember("identity");
        Assert.Equal("identity", character.Definition.Id);
    }

    private sealed class SelectingRandom(int selectedIndex) : IRandomService
    {
        public (int Minimum, int Maximum)? LastRange { get; private set; }

        public double NextDouble() => 0d;

        public int Next(int minInclusive, int maxExclusive)
        {
            LastRange = (minInclusive, maxExclusive);
            return selectedIndex;
        }
    }
}
