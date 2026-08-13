using Game.Core.Model;

namespace Game.Core.Battle;

public sealed class BasicEnemyBattleAgent : IBattleAgent
{
    private readonly BattleTurnCandidateGenerator _candidateGenerator;
    private readonly IBattleAiPolicyResolver _policyResolver;

    public BasicEnemyBattleAgent(
        BattleTurnCandidateGenerator candidateGenerator,
        IBattleAiPolicyResolver policyResolver)
    {
        _candidateGenerator = candidateGenerator ?? throw new ArgumentNullException(nameof(candidateGenerator));
        _policyResolver = policyResolver ?? throw new ArgumentNullException(nameof(policyResolver));
    }

    public BattleTurnPlan Decide(BattleState state, string unitId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(unitId);

        var unit = state.GetUnit(unitId);
        var isLowHp = unit.MaxHp > 0 && (double)unit.Hp / unit.MaxHp < 0.3d;
        var restRecovery = ResolveRestRecovery(unit);
        var policy = _policyResolver.Resolve(unit.AiType);
        var generatedCandidates = policy.GenerateCandidates(state, unit, _candidateGenerator);
        if (unit.AiType != BattleAiType.RestOnly &&
            generatedCandidates.All(candidate => candidate.Plan.MainAction.Kind != BattleMainActionKind.CastSkill) &&
            _candidateGenerator.ShouldUseSupportSpecialSkill())
        {
            var supportPositionCandidate = SelectBestRestPosition(generatedCandidates, isLowHp);
            if (supportPositionCandidate is not null &&
                _candidateGenerator.CreateRandomSupportSpecialSkillPlan(
                    state,
                    unit.Id,
                    supportPositionCandidate.Plan.MoveDestination) is { } supportPlan)
            {
                return supportPlan;
            }
        }

        BattleTurnCandidate? bestCandidate = null;
        var bestScore = double.NegativeInfinity;
        foreach (var candidate in generatedCandidates)
        {
            var score = ScoreCandidate(candidate, isLowHp, restRecovery);
            if (bestCandidate is null || IsBetterCandidate(candidate, score, bestCandidate, bestScore, isLowHp))
            {
                bestCandidate = candidate;
                bestScore = score;
            }
        }

        return bestCandidate?.Plan ??
            new BattleTurnPlan(unitId, unit.Position, BattleMainActionPlan.Rest());
    }

    private static double ScoreCandidate(BattleTurnCandidate candidate, bool isLowHp, int restRecovery)
    {
        var score = (double)(candidate.EnemyDamage - candidate.AllyDamage);
        score += candidate.EnemyKills * 5000d;
        score -= candidate.AllyKills * 8000d;
        if (candidate.EnemyHitCount > 1)
        {
            score += (candidate.EnemyHitCount - 1) * 400d;
        }

        if (candidate.Plan.MainAction.Kind == BattleMainActionKind.Rest)
        {
            score += restRecovery;
            score += isLowHp ? 1500d : -800d;
        }

        return score;
    }

    private static int ResolveRestRecovery(BattleUnit unit)
    {
        var recovery = BattleRestCalculator.EstimateAverage(unit);
        return recovery.Hp + recovery.Mp;
    }

    private static bool IsBetterCandidate(
        BattleTurnCandidate candidate,
        double score,
        BattleTurnCandidate currentBest,
        double currentBestScore,
        bool isLowHp)
    {
        if (score != currentBestScore)
        {
            return score > currentBestScore;
        }

        if (candidate.EnemyKills != currentBest.EnemyKills)
        {
            return candidate.EnemyKills > currentBest.EnemyKills;
        }

        if (candidate.EnemyDamage != currentBest.EnemyDamage)
        {
            return candidate.EnemyDamage > currentBest.EnemyDamage;
        }

        if (candidate.AllyDamage != currentBest.AllyDamage)
        {
            return candidate.AllyDamage < currentBest.AllyDamage;
        }

        var candidateActionKind = candidate.Plan.MainAction.Kind;
        var currentBestActionKind = currentBest.Plan.MainAction.Kind;
        if (candidateActionKind == currentBestActionKind)
        {
            if (candidate.DistanceToNearestEnemy != currentBest.DistanceToNearestEnemy)
            {
                return candidateActionKind == BattleMainActionKind.CastSkill || isLowHp
                    ? candidate.DistanceToNearestEnemy > currentBest.DistanceToNearestEnemy
                    : candidate.DistanceToNearestEnemy < currentBest.DistanceToNearestEnemy;
            }

            if (candidate.MoveCost != currentBest.MoveCost)
            {
                return candidate.MoveCost < currentBest.MoveCost;
            }
        }
        else
        {
            return candidateActionKind == BattleMainActionKind.CastSkill;
        }

        var candidatePlan = candidate.Plan;
        var currentBestPlan = currentBest.Plan;
        var comparison = candidatePlan.MoveDestination.Y.CompareTo(currentBestPlan.MoveDestination.Y);
        if (comparison != 0)
        {
            return comparison < 0;
        }

        comparison = candidatePlan.MoveDestination.X.CompareTo(currentBestPlan.MoveDestination.X);
        if (comparison != 0)
        {
            return comparison < 0;
        }

        comparison = (candidatePlan.MainAction.TargetPosition?.Y ?? int.MinValue)
            .CompareTo(currentBestPlan.MainAction.TargetPosition?.Y ?? int.MinValue);
        if (comparison != 0)
        {
            return comparison < 0;
        }

        comparison = (candidatePlan.MainAction.TargetPosition?.X ?? int.MinValue)
            .CompareTo(currentBestPlan.MainAction.TargetPosition?.X ?? int.MinValue);
        if (comparison != 0)
        {
            return comparison < 0;
        }

        return string.Compare(
            candidatePlan.MainAction.SkillId ?? string.Empty,
            currentBestPlan.MainAction.SkillId ?? string.Empty,
            StringComparison.Ordinal) < 0;
    }

    private static BattleTurnCandidate? SelectBestRestPosition(
        IReadOnlyList<BattleTurnCandidate> candidates,
        bool isLowHp)
    {
        BattleTurnCandidate? bestCandidate = null;
        foreach (var candidate in candidates)
        {
            if (candidate.Plan.MainAction.Kind != BattleMainActionKind.Rest ||
                bestCandidate is not null && !IsBetterRestPosition(candidate, bestCandidate, isLowHp))
            {
                continue;
            }

            bestCandidate = candidate;
        }

        return bestCandidate;
    }

    private static bool IsBetterRestPosition(
        BattleTurnCandidate candidate,
        BattleTurnCandidate currentBest,
        bool isLowHp)
    {
        if (candidate.DistanceToNearestEnemy != currentBest.DistanceToNearestEnemy)
        {
            return isLowHp
                ? candidate.DistanceToNearestEnemy > currentBest.DistanceToNearestEnemy
                : candidate.DistanceToNearestEnemy < currentBest.DistanceToNearestEnemy;
        }

        if (candidate.MoveCost != currentBest.MoveCost)
        {
            return candidate.MoveCost < currentBest.MoveCost;
        }

        var candidatePosition = candidate.Plan.MoveDestination;
        var currentBestPosition = currentBest.Plan.MoveDestination;
        return candidatePosition.Y != currentBestPosition.Y
            ? candidatePosition.Y < currentBestPosition.Y
            : candidatePosition.X < currentBestPosition.X;
    }
}
