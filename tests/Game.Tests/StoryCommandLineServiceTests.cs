using Game.Application;
using Game.Core.Definitions;
using Game.Core.Definitions.Skills;
using Game.Core.Model;
using Game.Core.Story;

namespace Game.Tests;

public sealed class StoryCommandLineServiceTests
{
	[Fact]
	public void Parse_SupportsQuotedStringsBooleansAndNumbers()
	{
		var service = CreateService(out _);

		var invocation = service.Parse("custom_cmd \"hello world\" true -3 1.5");

		Assert.Equal("custom_cmd", invocation.Name);
		Assert.Equal(4, invocation.Arguments.Count);
		Assert.Equal("hello world", invocation.Arguments[0].AsString("arg0"));
		Assert.True(invocation.Arguments[1].AsBoolean("arg1"));
		Assert.Equal(-3d, invocation.Arguments[2].AsNumber("arg2"));
		Assert.Equal(1.5d, invocation.Arguments[3].AsNumber("arg3"));
	}

	[Fact]
	public async Task ExecuteAsync_DispatchesBuiltInAndHostCommands()
	{
		var service = CreateService(out var session);

		await service.ExecuteAsync("log \"踏入江湖\"");
		await service.ExecuteAsync("map town");

		var entry = Assert.Single(session.State.Journal.Entries);
		Assert.Equal("踏入江湖", entry.Text);

		var host = Assert.IsType<RecordingRuntimeHost>(session.StoryService.Host);
		var command = Assert.Single(host.Commands);
		Assert.Equal("map", command.Name);
		Assert.Equal("town", command.Args[0].AsString("map"));
	}

	[Fact]
	public async Task ExecuteAsync_CostMoneyRejectsNegativeAmount()
	{
		var service = CreateService(out var session);

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await service.ExecuteAsync("cost_money -100"));

		Assert.Equal(0, session.State.Currency.Silver);
	}

	[Fact]
	public async Task ExecuteAsync_SetNoRegretEnablesRuleAndPublishesEvent()
	{
		var service = CreateService(out var session);
		var events = new List<AdventureStateChangedEvent>();
		using var subscription = session.Events.Subscribe<AdventureStateChangedEvent>(events.Add);

		await service.ExecuteAsync("set_no_regret");

		Assert.True(session.State.Adventure.NoRegret);
		Assert.Single(events);
	}

	[Fact]
	public async Task ExecuteAsync_LearnDoesNotDowngradeKnownSkills()
	{
		var externalSkill = TestContentFactory.CreateExternalSkill("external");
		var internalSkill = TestContentFactory.CreateInternalSkill("internal");
		var heroDefinition = TestContentFactory.CreateCharacterDefinition(
			"hero",
			externalSkills: [new InitialExternalSkillEntryDefinition(externalSkill, Level: 8)],
			internalSkills: [new InitialInternalSkillEntryDefinition(internalSkill, Level: 9)]);
		var state = new GameState();
		var hero = TestContentFactory.CreateCharacterInstance(
			"hero",
			heroDefinition,
			state.EquipmentInstanceFactory);
		state.Party.AddMember(hero);
		var repository = TestContentFactory.CreateRepository(
			characters: [heroDefinition],
			externalSkills: [externalSkill],
			internalSkills: [internalSkill]);
		var session = new GameSession(state, repository, new RecordingRuntimeHost());

		await session.StoryService.CommandLine.ExecuteAsync("learn skill hero external 3");
		await session.StoryService.CommandLine.ExecuteAsync("learn internal hero internal 4");

		Assert.Equal(8, hero.GetExternalSkillLevel(externalSkill.Id));
		Assert.Equal(9, hero.GetInternalSkillLevel(internalSkill.Id));
	}

	[Fact]
	public async Task ExecuteAsync_JoinAcceptsExplicitDefinitionId()
	{
		var definition = TestContentFactory.CreateCharacterDefinition("chengying.low");
		var repository = TestContentFactory.CreateRepository(characters: [definition]);
		var session = new GameSession(new GameState(), repository, new RecordingRuntimeHost());

		await session.StoryService.CommandLine.ExecuteAsync("join chengying chengying.low");

		var character = Assert.Single(session.State.Party.Members);
		Assert.Equal("chengying", character.Id);
		Assert.Same(definition, character.Definition);
	}

	private static StoryCommandLineService CreateService(out GameSession session)
	{
		var repository = TestContentFactory.CreateRepository(
			maps:
			[
				new MapDefinition
				{
					Id = "town",
					Name = "town",
					Kind = MapKind.Small,
				},
			]);
		var host = new RecordingRuntimeHost();
		session = new GameSession(new GameState(), repository, host);
		return session.StoryService.CommandLine;
	}

	private sealed class RecordingRuntimeHost : IRuntimeHost
	{
		public List<(string Name, IReadOnlyList<ExprValue> Args)> Commands { get; } = [];

		public ValueTask DialogueAsync(DialogueContext dialogue, CancellationToken cancellationToken) =>
			ValueTask.CompletedTask;

		public ValueTask<ExprValue> GetVariableAsync(string name, CancellationToken cancellationToken) =>
			ValueTask.FromException<ExprValue>(new InvalidOperationException(name));

		public ValueTask<bool> EvaluatePredicateAsync(
			string name,
			IReadOnlyList<ExprValue> args,
			CancellationToken cancellationToken) =>
			ValueTask.FromException<bool>(new InvalidOperationException(name));

		public ValueTask<StoryCommandResult> ExecuteCommandAsync(
			string name,
			IReadOnlyList<ExprValue> args,
			CancellationToken cancellationToken)
		{
			Commands.Add((name, args));
			return ValueTask.FromResult(StoryCommandResult.None);
		}

		public ValueTask<int> ChooseOptionAsync(ChoiceContext choice, CancellationToken cancellationToken) =>
			ValueTask.FromResult(0);

		public ValueTask<BattleOutcome> ResolveBattleAsync(BattleContext battle, CancellationToken cancellationToken) =>
			ValueTask.FromResult(BattleOutcome.Win);
	}
}
