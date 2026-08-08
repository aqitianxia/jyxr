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

    [Fact]
    public void ReplaceState_RejectsReservedVariablesWithoutReplacingTheCurrentState()
    {
        var original = new GameState();
        var session = new GameSession(original, TestContentFactory.CreateRepository());
        var invalid = new GameState();
        invalid.Story.SetVariable("silver", ExpressionValue.FromNumber(1));

        Assert.Throws<InvalidOperationException>(() => session.ReplaceState(invalid));
        Assert.Same(original, session.State);
    }

    [Fact]
    public async Task Service_ResolvesJumpAndCallTargetsAcrossStoryFiles()
    {
        var first = StoryScriptJson.Parse("""
        {"version":3,"segments":[{"name":"start","steps":[
          {"kind":"call","target":"shared"},
          {"kind":"jump","target":"finish"}
        ]}]}
        """, "first.story.json");
        var second = StoryScriptJson.Parse("""
        {"version":3,"segments":[
          {"name":"shared","steps":[{"kind":"dialogue","speaker":"主角","text":"共享片段"},{"kind":"return"}]},
          {"name":"finish","steps":[{"kind":"dialogue","speaker":"主角","text":"结束"}]}
        ]}
        """, "second.story.json");
        var host = new RecordingHost();
        var session = new GameSession(new GameState(), TestContentFactory.CreateRepository(storyScripts: [first, second]), host);

        await session.StoryService.ExecuteAsync("start");

        Assert.Equal(["共享片段", "结束"], host.DialogueTexts);
        Assert.True(session.State.Story.IsStoryCompleted("shared"));
        Assert.True(session.State.Story.IsStoryCompleted("finish"));
    }

    [Fact]
    public async Task Runtime_CallReturnsThroughNestedBranchAndChoicePreservesSourceIndex()
    {
        var script = StoryScriptJson.Parse("""
        {"version":3,"segments":[
          {"name":"start","steps":[
            {"kind":"call","target":"sub"},
            {"kind":"choice","prompt":{"speaker":"主角","text":"选择"},"groups":[
              {"when":"false","options":[{"text":"隐藏","steps":[]}]},
              {"options":[{"text":"可见","steps":[{"kind":"dialogue","speaker":"主角","text":"选择完成"}]}]}
            ]}
          ]},
          {"name":"sub","steps":[
            {"kind":"branch","cases":[{"when":"true","steps":[{"kind":"return"}]}]},
            {"kind":"dialogue","speaker":"主角","text":"不应执行"}
          ]}
        ]}
        """);
        var host = new RecordingHost { SelectedOptionIndex = 1 };
        var session = new GameSession(new GameState(), TestContentFactory.CreateRepository(storyScripts: [script]), host);

        await session.StoryService.ExecuteAsync("start");

        Assert.Equal(["选择完成"], host.DialogueTexts);
        Assert.Equal([1], host.OfferedOptionIndices);
    }

    [Fact]
    public async Task Runtime_RejectsChoiceWithoutAnyVisibleOption()
    {
        var script = StoryScriptJson.Parse("""
        {"version":3,"segments":[{"name":"start","steps":[
          {"kind":"choice","prompt":{"speaker":"主角","text":"无路可走"},"groups":[
            {"when":"false","options":[{"text":"隐藏","steps":[]}]}
          ]}
        ]}]}
        """);
        var session = new GameSession(new GameState(), TestContentFactory.CreateRepository(storyScripts: [script]));

        var exception = await Assert.ThrowsAsync<StoryRuntimeException>(() =>
            session.StoryService.ExecuteAsync("start"));
        Assert.Contains("no available options", exception.Message);
    }

    [Theory]
    [InlineData(BattleOutcome.Win, false, true)]
    [InlineData(BattleOutcome.Lose, true, false)]
    public async Task Runtime_HandlesBattleOutcomesWithoutExplicitBranches(
        BattleOutcome outcome,
        bool expectedGameOver,
        bool expectedContinuation)
    {
        var script = StoryScriptJson.Parse("""
        {"version":3,"segments":[{"name":"start","steps":[
          {"kind":"battle","battleId":"test","outcomes":{}},
          {"kind":"dialogue","speaker":"主角","text":"继续"}
        ]}]}
        """);
        var host = new RecordingHost { BattleOutcome = outcome };
        var session = new GameSession(new GameState(), TestContentFactory.CreateRepository(storyScripts: [script]), host);

        await session.StoryService.ExecuteAsync("start");

        Assert.Equal(expectedGameOver, host.GameOverInvoked);
        Assert.Equal(expectedContinuation, host.DialogueTexts.Contains("继续"));
    }

    private sealed class RecordingHost : IRuntimeHost
    {
        public List<string> DialogueTexts { get; } = [];
        public List<int> OfferedOptionIndices { get; } = [];
        public int SelectedOptionIndex { get; init; }
        public BattleOutcome BattleOutcome { get; init; } = BattleOutcome.Win;
        public bool GameOverInvoked { get; private set; }
        public ValueTask DialogueAsync(DialogueContext dialogue, CancellationToken cancellationToken) { DialogueTexts.Add(dialogue.Text); return ValueTask.CompletedTask; }
        public ValueTask<int> ChooseOptionAsync(ChoiceContext choice, CancellationToken cancellationToken)
        {
            OfferedOptionIndices.AddRange(choice.Options.Select(static option => option.Index));
            return ValueTask.FromResult(SelectedOptionIndex);
        }
        public ValueTask<BattleOutcome> ResolveBattleAsync(BattleContext battle, CancellationToken cancellationToken) => ValueTask.FromResult(BattleOutcome);
        public ValueTask GameOverAsync(CancellationToken cancellationToken) { GameOverInvoked = true; return ValueTask.CompletedTask; }
    }
}
