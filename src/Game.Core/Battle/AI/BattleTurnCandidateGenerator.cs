using Game.Core.Model;
using Game.Core.Model.Skills;
using Game.Core.Definitions.Skills;

namespace Game.Core.Battle;

public sealed class BattleTurnCandidateGenerator
{
    private readonly BattleEngine _engine;
    private readonly IReadOnlyList<IBattleSkillAiScorer> _skillScorers;

    public BattleTurnCandidateGenerator(
        BattleEngine engine,
        IReadOnlyList<IBattleSkillAiScorer>? skillScorers = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _skillScorers = skillScorers ?? [new DamageSkillAiScorer(), new SpecialSkillAiScorer()];
    }

    public IReadOnlyList<BattleTurnCandidate> Generate(
        BattleState state,
        string unitId,
        BattleTurnCandidateGenerationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(unitId);

        options ??= BattleTurnCandidateGenerationOptions.Default;
        var unit = state.GetUnit(unitId);
        IReadOnlyDictionary<GridPosition, int> reachablePositions = options.AllowMovement
            ? _engine.GetReachablePositions(state, unitId)
            : new Dictionary<GridPosition, int> { [unit.Position] = 0 };
        var preparedSkills = options.AllowSkillCandidates
            ? PrepareSkills(state, unit, options.SkillFilter)
            : [];
        var enemyPositions = state.Units
            .Where(other => other.IsAlive && state.AreEnemies(unit, other))
            .Select(static other => other.Position)
            .ToArray();
        var candidates = new List<BattleTurnCandidate>();

        foreach (var (destination, moveCost) in reachablePositions)
        {
            var distanceToNearestEnemy = GetDistanceToNearestEnemy(enemyPositions, destination);
            if (options.AllowSkillCandidates)
            {
                foreach (var preparedSkill in preparedSkills)
                {
                    var skill = preparedSkill.Skill;
                    foreach (var target in BattleSkillTargeting.EnumerateCastTargets(
                                 destination,
                                 preparedSkill.CastSize,
                                 skill.CanCastAtSelf,
                                 state.Grid))
                    {
                        var impactedPositions = BattleEngine.GetImpactPositions(
                                destination,
                                target,
                                skill.ImpactType,
                                preparedSkill.ImpactSize)
                            .Where(state.Grid.Contains)
                            .ToHashSet();
						var targets = BattleSkillTargeting.ResolveEffectiveTargets(
							state,
							unit,
							skill,
							impactedPositions);
                        if (targets.Count == 0 || targets.All(targetUnit => !state.AreEnemies(unit, targetUnit)))
                        {
                            continue;
                        }

                        var evaluation = preparedSkill.Scorer.Score(new BattleSkillAiContext(
                            state,
                            unit,
                            skill,
                            destination,
                            target,
                            targets));
                        if (skill is SpecialSkillInstance && evaluation.EnemyDamage <= 0)
                        {
                            continue;
                        }
                        candidates.Add(new BattleTurnCandidate(
                            new BattleTurnPlan(unit.Id, destination, BattleMainActionPlan.CastSkill(skill.Id, target)),
                            Score: 0d,
                            evaluation.EnemyDamage,
                            evaluation.AllyDamage,
                            evaluation.EnemyKills,
                            evaluation.AllyKills,
                            evaluation.EnemyHitCount,
                            DistanceToNearestEnemy: distanceToNearestEnemy,
                            MoveCost: moveCost));
                    }
                }
            }

            if (options.AllowRestCandidates)
            {
                candidates.Add(new BattleTurnCandidate(
                    new BattleTurnPlan(unit.Id, destination, BattleMainActionPlan.Rest()),
                    Score: 0d,
                    EnemyDamage: 0,
                    AllyDamage: 0,
                    EnemyKills: 0,
                    AllyKills: 0,
                    EnemyHitCount: 0,
                    DistanceToNearestEnemy: distanceToNearestEnemy,
                    MoveCost: moveCost));
            }
        }

        return candidates;
    }

    public BattleTurnPlan? CreateRandomSupportSpecialSkillPlan(
        BattleState state,
        string unitId,
        GridPosition destination)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(unitId);

        var unit = state.GetUnit(unitId);
        var skills = unit.Character.GetSpecialSkills()
            .Where(skill =>
                skill.IsActive &&
                skill.Definition.Intent == SpecialSkillIntent.Support &&
                skill.CanCastAtSelf &&
                _engine.EvaluateSkillAvailability(state, unit.Id, skill).IsAvailable)
            .ToArray();
        if (skills.Length == 0)
        {
            return null;
        }

        var skill = skills[_engine.RandomService.Next(0, skills.Length)];
        return new BattleTurnPlan(
            unit.Id,
            destination,
            BattleMainActionPlan.CastSkill(skill.Id, destination));
    }

    internal bool ShouldUseSupportSpecialSkill() =>
        _engine.RandomService.Next(0, 2) == 0;

    private IReadOnlyList<PreparedSkill> PrepareSkills(
        BattleState state,
        BattleUnit unit,
        Func<SkillInstance, bool>? skillFilter)
    {
        var preparedSkills = new List<PreparedSkill>();
        foreach (var skill in BattleSkillCatalog.CollectSelectableSkills(unit))
        {
            if (skillFilter is not null && !skillFilter(skill))
            {
                continue;
            }

            if (skill is SpecialSkillInstance specialSkill &&
                specialSkill.Definition.Intent != SpecialSkillIntent.Offensive)
            {
                continue;
            }

            if (!_engine.EvaluateSkillAvailability(state, unit.Id, skill).IsAvailable)
            {
                continue;
            }

            var scorer = _skillScorers.FirstOrDefault(candidateScorer => candidateScorer.CanScore(skill));
            if (scorer is null)
            {
                continue;
            }

            preparedSkills.Add(new PreparedSkill(
                skill,
                scorer,
                BattleSkillTargeting.ResolveEffectiveCastSize(unit, skill),
                BattleSkillTargeting.ResolveEffectiveImpactSize(unit, skill)));
        }

        return preparedSkills;
    }

    private static int GetDistanceToNearestEnemy(
        IReadOnlyList<GridPosition> enemyPositions,
        GridPosition destination) =>
        enemyPositions.Count == 0
            ? 0
            : enemyPositions.Min(destination.ManhattanDistanceTo);

    private sealed record PreparedSkill(
        SkillInstance Skill,
        IBattleSkillAiScorer Scorer,
        int CastSize,
        int ImpactSize);
}
