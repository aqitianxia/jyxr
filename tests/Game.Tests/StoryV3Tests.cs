using Game.Application;
using Game.Core.Model;
using Game.Core.Story;

namespace Game.Tests;

public sealed class StoryV3Tests
{
    [Fact]
    public void JsonParser_ParsesV3CallBranchAndChoice()
    {
        const string json = """
        {"version":3,"segments":[{"name":"start","steps":[
          {"kind":"command","call":"set_var('quest_stage', 3)"},
          {"kind":"branch","cases":[{"when":"quest_stage >= 3","steps":[]}]},
          {"kind":"choice","prompt":{"speaker":"主角","text":"走吗"},"groups":[{"when":"has_var('quest_stage')","options":[{"text":"走","steps":[]}]}]}
        ]}]}
        """;

        var script = StoryScriptJson.Parse(json, "v3-test");

        Assert.Equal(3, script.Version);
        Assert.IsType<CommandStep>(script.Segments[0].Steps[0]);
        Assert.IsType<BranchStep>(script.Segments[0].Steps[1]);
        Assert.IsType<ChoiceStep>(script.Segments[0].Steps[2]);
    }

    [Theory]
    [InlineData("{\"version\":2,\"segments\":[]}")]
    [InlineData("{\"version\":3,\"segments\":[{\"name\":\"x\",\"steps\":[{\"kind\":\"command\",\"name\":\"journal\",\"args\":[]}]}]}")]
    [InlineData("{\"version\":3,\"segments\":[{\"name\":\"x\",\"steps\":[{\"kind\":\"command\",\"call\":[\"journal\"]}]}]}")]
    public void JsonParser_RejectsV2AndOldCommandShapes(string json) =>
        Assert.Throws<StoryRuntimeException>(() => StoryScriptJson.Parse(json, "invalid-story"));

    [Fact]
    public async Task Service_ExecutesStrictVariablesBranchChoiceAndJump()
    {
        const string json = """
        {"version":3,"segments":[
          {"name":"start","steps":[
            {"kind":"command","call":"set_var('quest_stage', 1)"},
            {"kind":"command","call":"change_var('quest_stage', 2)"},
            {"kind":"branch","cases":[{"when":"quest_stage == 3","steps":[{"kind":"jump","target":"end"}]}]}
          ]},
          {"name":"end","steps":[{"kind":"dialogue","speaker":"主角","text":"完成"}]}
        ]}
        """;
        var script = StoryScriptJson.Parse(json);
        var host = new RecordingHost();
        var session = new GameSession(new GameState(), TestContentFactory.CreateRepository(storyScripts: [script]), host);

        await session.StoryService.ExecuteAsync("start");

        Assert.Equal(3, session.State.Story.Variables["quest_stage"].AsNumber("test"));
        Assert.Contains("完成", host.DialogueTexts);
        Assert.True(session.State.Story.IsStoryCompleted("end"));
    }

    [Fact]
    public async Task Service_ExecutionContextIsIsolatedAndRequired()
    {
        const string json = """
        {"version":3,"segments":[{"name":"item","steps":[{"kind":"branch","cases":[{"when":"item_target == 'hero'","steps":[{"kind":"command","call":"journal('ok')"}]}]}]}]}
        """;
        var script = StoryScriptJson.Parse(json);
        var session = new GameSession(new GameState(), TestContentFactory.CreateRepository(storyScripts: [script]));
        var context = new StoryExecutionContext(new Dictionary<string, ExpressionValue> { ["item_target"] = ExpressionValue.FromString("hero") });

        await session.StoryService.ExecuteAsync("item", context);
        Assert.Single(session.State.Journal.Entries);
        await Assert.ThrowsAsync<ExpressionEvaluationException>(() => session.StoryService.ExecuteAsync("item"));
    }

    [Fact]
    public async Task DynamicVariablesEnforceTypeAndReservedNames()
    {
        var session = new GameSession(new GameState(), TestContentFactory.CreateRepository());
        await session.StoryService.CommandDispatcher.ExecuteCommandAsync("set_var", [ExpressionValue.FromString("flag"), ExpressionValue.FromBoolean(true)]);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await session.StoryService.CommandDispatcher.ExecuteCommandAsync("set_var", [ExpressionValue.FromString("flag"), ExpressionValue.FromNumber(1)]));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await session.StoryService.CommandDispatcher.ExecuteCommandAsync("set_var", [ExpressionValue.FromString("silver"), ExpressionValue.FromNumber(1)]));
    }

    private sealed class RecordingHost : IRuntimeHost
    {
        public List<string> DialogueTexts { get; } = [];
        public ValueTask DialogueAsync(DialogueContext dialogue, CancellationToken cancellationToken) { DialogueTexts.Add(dialogue.Text); return ValueTask.CompletedTask; }
        public ValueTask<int> ChooseOptionAsync(ChoiceContext choice, CancellationToken cancellationToken) => ValueTask.FromResult(choice.Options[0].Index);
        public ValueTask<BattleOutcome> ResolveBattleAsync(BattleContext battle, CancellationToken cancellationToken) => ValueTask.FromResult(BattleOutcome.Win);
    }
}
