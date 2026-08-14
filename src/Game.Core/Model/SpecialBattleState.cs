using Game.Core.Persistence;

namespace Game.Core.Model;

public sealed class SpecialBattleState
{
    private readonly HashSet<string> _trialCompletedCharacterIds = new(StringComparer.Ordinal);
    private readonly Dictionary<TowerRewardKey, int> _towerRewardClaimCounts = [];

    public IReadOnlyCollection<string> TrialCompletedCharacterIds => _trialCompletedCharacterIds;

    public IReadOnlyDictionary<TowerRewardKey, int> TowerRewardClaimCounts => _towerRewardClaimCounts;

    public static SpecialBattleState Restore(SpecialBattleStateRecord? record)
    {
        var state = new SpecialBattleState();
        if (record is null)
        {
            return state;
        }

        foreach (var characterId in record.TrialCompletedCharacterIds)
        {
            state.MarkTrialCompleted(characterId);
        }

        foreach (var claim in record.TowerRewardClaims ?? [])
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(claim.TowerId);
            ArgumentException.ThrowIfNullOrWhiteSpace(claim.StageId);
            ArgumentException.ThrowIfNullOrWhiteSpace(claim.RewardId);
            ArgumentOutOfRangeException.ThrowIfNegative(claim.Count);
            if (claim.Count > 0)
            {
                state._towerRewardClaimCounts.Add(
                    new TowerRewardKey(claim.TowerId, claim.StageId, claim.RewardId),
                    claim.Count);
            }
        }

        return state;
    }

    public bool IsTrialCompleted(string characterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(characterId);
        return _trialCompletedCharacterIds.Contains(characterId);
    }

    public bool MarkTrialCompleted(string characterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(characterId);
        return _trialCompletedCharacterIds.Add(characterId);
    }

    public int GetTowerRewardClaimCount(string towerId, string stageId, string rewardId)
    {
        var claimKey = CreateTowerRewardKey(towerId, stageId, rewardId);
        return _towerRewardClaimCounts.GetValueOrDefault(claimKey);
    }

    public void AddTowerRewardClaim(string towerId, string stageId, string rewardId)
    {
        var claimKey = CreateTowerRewardKey(towerId, stageId, rewardId);
        _towerRewardClaimCounts[claimKey] = GetTowerRewardClaimCount(towerId, stageId, rewardId) + 1;
    }

    public SpecialBattleStateRecord ToRecord() =>
        new(
            _trialCompletedCharacterIds.OrderBy(static id => id, StringComparer.Ordinal).ToArray(),
            _towerRewardClaimCounts
                .OrderBy(static entry => entry.Key.TowerId, StringComparer.Ordinal)
                .ThenBy(static entry => entry.Key.StageId, StringComparer.Ordinal)
                .ThenBy(static entry => entry.Key.RewardId, StringComparer.Ordinal)
                .Select(static entry => new TowerRewardClaimRecord(
                    entry.Key.TowerId,
                    entry.Key.StageId,
                    entry.Key.RewardId,
                    entry.Value))
                .ToArray());

    private static TowerRewardKey CreateTowerRewardKey(string towerId, string stageId, string rewardId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(towerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rewardId);
        return new TowerRewardKey(towerId, stageId, rewardId);
    }
}

public readonly record struct TowerRewardKey(string TowerId, string StageId, string RewardId);
