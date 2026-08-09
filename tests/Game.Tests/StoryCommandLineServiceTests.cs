using Game.Application;
using Game.Core.Model;

namespace Game.Tests;

public sealed class StoryCommandLineServiceTests
{
    [Fact]
    public void Parse_SupportsCanonicalThinTokens()
    {
        var service = CreateService(out _);
        var invocation = service.Parse("sample_command \"hello world\" true");
        Assert.Equal("sample_command", invocation.Name);
        Assert.Equal("hello world", invocation.Arguments[0].AsString("test"));
        Assert.True(invocation.Arguments[1].AsBoolean("test"));
    }

    [Fact]
    public void Parse_PreservesQuotedScalarStringsAndParenthesesInThinArguments()
    {
        var service = CreateService(out _);

        var invocation = service.Parse("sample_command 'true' '123'");
        Assert.Equal("true", invocation.Arguments[0].AsString("test"));
        Assert.Equal("123", invocation.Arguments[1].AsString("test"));

        var parentheses = service.Parse("journal 丹药(大)");
        Assert.Equal("丹药(大)", parentheses.Arguments[0].AsString("test"));
    }

    [Fact]
    public async Task ExecuteAsync_SupportsThinAndFullDsl()
    {
        var service = CreateService(out var session);
        await service.ExecuteAsync("change_silver 10");
        await service.ExecuteAsync("journal('踏入江湖')");
        Assert.Equal(10, session.State.Currency.Silver);
        Assert.Equal("踏入江湖", Assert.Single(session.State.Journal.Entries).Text);
    }

    [Fact]
    public async Task ExecuteAsync_SupportsApprovedAliasAndRejectsAdapterAlias()
    {
        var service = CreateService(out var session);
        await service.ExecuteAsync("get_money 10");
        Assert.Equal(10, session.State.Currency.Silver);
        await Assert.ThrowsAsync<ExpressionBindingException>(async () => await service.ExecuteAsync("cost_money 10"));
    }

    [Fact]
    public async Task ExecuteAsync_BooleanParametersAreStrict()
    {
        var service = CreateService(out var session);
        await service.ExecuteAsync("set_no_regret true");
        Assert.True(session.State.Adventure.NoRegret);
        await Assert.ThrowsAsync<ExpressionBindingException>(async () => await service.ExecuteAsync("set_no_regret 1"));
    }

    private static StoryCommandLineService CreateService(out GameSession session)
    {
        session = new GameSession(new GameState(), TestContentFactory.CreateRepository());
        return session.StoryService.CommandLine;
    }
}
