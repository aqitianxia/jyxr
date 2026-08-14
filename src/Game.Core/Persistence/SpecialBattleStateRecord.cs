namespace Game.Core.Persistence;

public sealed record SpecialBattleStateRecord(
    IReadOnlyList<string> TrialCompletedCharacterIds,
    IReadOnlyList<TowerRewardClaimRecord>? TowerRewardClaims = null);

public sealed record TowerRewardClaimRecord(
    string TowerId,
    string StageId,
    string RewardId,
    int Count);
