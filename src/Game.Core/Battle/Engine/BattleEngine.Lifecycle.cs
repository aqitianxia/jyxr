using Game.Core.Affix;

namespace Game.Core.Battle;

public sealed partial class BattleEngine
{
    public BattleCommandResult<BattleState> StartBattle(BattleState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        using var command = state.BeginCommand();
        if (state.HasStarted)
            return BattleCommandResult<BattleState>.Succeeded(state, command.Messages, "Battle already started.");

        state.HasStarted = true;
        foreach (var unit in state.Units.Where(static unit => unit.IsAlive))
        {
            TriggerHooks(state, HookTiming.OnBattleStart, unit);
        }
        return BattleCommandResult<BattleState>.Succeeded(state, command.Messages, "Battle started.");
    }
}
