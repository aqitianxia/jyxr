using Game.Core.Story;

namespace Game.Application;

internal sealed class NullRuntimeHost : IRuntimeHost
{
    public static NullRuntimeHost Instance { get; } = new();
    private NullRuntimeHost() { }

    public ValueTask DialogueAsync(DialogueContext dialogue, CancellationToken cancellationToken) => Fail();
    public ValueTask<int> ChooseOptionAsync(ChoiceContext choice, CancellationToken cancellationToken) => ValueTask.FromException<int>(Error());
    public ValueTask<BattleOutcome> ResolveBattleAsync(BattleContext battle, CancellationToken cancellationToken) => ValueTask.FromException<BattleOutcome>(Error());
    public ValueTask PlayEffectAsync(string effectId, CancellationToken cancellationToken) => Fail();
    public ValueTask GameOverAsync(CancellationToken cancellationToken) => Fail();

    private static InvalidOperationException Error() => new("Story runtime host is not configured.");
    private static ValueTask Fail() => ValueTask.FromException(Error());
}
