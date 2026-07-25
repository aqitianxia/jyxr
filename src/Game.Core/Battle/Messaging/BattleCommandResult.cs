using Game.Core.Model;

namespace Game.Core.Battle;

public sealed record BattleCommandResult<T>(
    bool Success,
    BattleCommandFailure? Failure,
    T? Value,
    IReadOnlyList<BattleMessage> Messages)
{
    public static BattleCommandResult<T> Succeeded(T value, IReadOnlyList<BattleMessage> messages) =>
        new(true, null, value, messages);

    public static BattleCommandResult<T> Failed(
        BattleCommandFailureReason reason,
        IReadOnlyList<BattleMessage>? messages = null,
        int? remainingTurns = null) =>
        new(false, new BattleCommandFailure(reason, remainingTurns), default, messages ?? []);
}

public sealed record BattleCommandFailure(
    BattleCommandFailureReason Reason,
    int? RemainingTurns = null);

public enum BattleCommandFailureReason
{
    UnitAlreadyActing,
    UnitDefeated,
    UnitNotReady,
    NoUnitActing,
    WrongActingUnit,
    MainActionCommitted,
    SkillOwnerMismatch,
    SkillInactive,
    SkillOnCooldown,
    SkillDisabled,
    SkillUnavailable,
    NotEnoughMp,
    NotEnoughRage,
    SkillCannotTargetSelf,
    TargetOutOfCastRange,
    InvalidItemTarget,
    ItemCannotTargetEnemy,
    ItemCannotTargetAlly,
    AllyItemTargetOutOfRange,
    ItemOnCooldown,
    DestinationUnreachable,
    MovementRollbackAfterMainAction,
    TimelineAdvanceLimitReached,
}

public sealed record BattleActionResult(
    IReadOnlyList<string> AffectedUnitIds,
    IReadOnlyList<GridPosition> ImpactedPositions,
    BattleSkillCastInfo? SkillCast = null);
