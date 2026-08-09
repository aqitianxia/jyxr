using Game.Core;
using Game.Core.Abstractions;
using Game.Core.Battle;
using Game.Core.Definitions;
using Game.Core.Definitions.Skills;
using Game.Core.Model;

namespace Game.Application;

public static class OrdinaryBattleLootGenerator
{
    public static IReadOnlyList<RewardGrant> Generate(
        BattleState state,
        IContentRepository contentRepository,
        GameConfig config,
        SkillMaxLevelPolicy skillMaxLevelPolicy,
        GameDifficulty difficulty,
        int round,
        int playerTeam,
        double dropChance,
        IRandomService random)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(contentRepository);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(skillMaxLevelPolicy);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfLessThan(round, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(dropChance, 0d);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(dropChance, 1d);

        var drops = new List<RewardGrant>();
        foreach (var enemyUnit in state.Units.Where(unit => unit.Team != playerTeam))
        {
            AddOrdinaryItemDrop(
                drops,
                contentRepository,
                config,
                round,
                random,
                dropChance,
                enemyUnit.Character.Level);
            AddSkillFragmentDrops(
                drops,
                contentRepository,
                config,
                skillMaxLevelPolicy,
                difficulty,
                round,
                enemyUnit.Character.Level);
        }

        return drops;
    }

    private static void AddOrdinaryItemDrop(
        List<RewardGrant> drops,
        IContentRepository contentRepository,
        GameConfig config,
        int round,
        IRandomService random,
        double dropChance,
        int enemyLevel)
    {
        if (!Probability.RollChance(dropChance))
        {
            return;
        }

        var candidates = ResolveDropCandidates(contentRepository, enemyLevel);
        if (candidates.Count == 0)
        {
            return;
        }

        var item = PickRandom(candidates);
        if (item is EquipmentDefinition equipment)
        {
            drops.Add(new EquipmentRewardGrant(
                equipment,
                GenerateEquipmentRolls(equipment, contentRepository, config, round, random)));
            return;
        }

        drops.Add(new StackItemRewardGrant(item, 1));
    }

    private static void AddSkillFragmentDrops(
        List<RewardGrant> drops,
        IContentRepository contentRepository,
        GameConfig config,
        SkillMaxLevelPolicy skillMaxLevelPolicy,
        GameDifficulty difficulty,
        int round,
        int enemyLevel)
    {
        var externalDropChance = ResolveExternalSkillFragmentDropChance(config, difficulty, round);
        if (Probability.RollChance(externalDropChance))
        {
            var candidates = ResolveExternalSkillFragmentCandidates(
                drops,
                contentRepository,
                config,
                skillMaxLevelPolicy,
                enemyLevel);
            if (candidates.Count > 0)
            {
                var skill = PickRandom(candidates);
                drops.Add(CreateExternalSkillFragmentDrop(skill));
            }
        }

        if (config.CanzhangDropRateInternalRate <= 0d)
        {
            return;
        }

        if (Probability.RollChance(externalDropChance / config.CanzhangDropRateInternalRate))
        {
            var candidates = ResolveInternalSkillFragmentCandidates(
                drops,
                contentRepository,
                config,
                skillMaxLevelPolicy,
                enemyLevel);
            if (candidates.Count > 0)
            {
                var skill = PickRandom(candidates);
                drops.Add(CreateInternalSkillFragmentDrop(skill));
            }
        }
    }

    private static double ResolveExternalSkillFragmentDropChance(
        GameConfig config,
        GameDifficulty difficulty,
        int round) =>
        difficulty switch
        {
            GameDifficulty.Hard => config.HardModeCanzhangDropRate,
            GameDifficulty.Crazy => config.CrazyModeCanzhangDropRate +
                                    (round - 1) * config.CrazyModeCanzhangDropRatePerRound,
            _ => 0d,
        };

    private static IReadOnlyList<ExternalSkillDefinition> ResolveExternalSkillFragmentCandidates(
        IReadOnlyList<RewardGrant> pendingRewards,
        IContentRepository contentRepository,
        GameConfig config,
        SkillMaxLevelPolicy skillMaxLevelPolicy,
        int enemyLevel)
    {
        var levelHardLimit = ResolveSkillFragmentLevelHardLimit(config, enemyLevel);
        return contentRepository.GetExternalSkills()
            .Where(skill => skill.Hard < config.CanzhangMaxHardSkill)
            .Where(skill => skill.Hard < levelHardLimit)
            .Where(skill => enemyLevel < 30 || skill.Hard >= 5d)
            .Where(skill => enemyLevel < 20 || skill.Hard >= 3d)
            .Where(skill => !IsExternalSkillMaxed(config, skillMaxLevelPolicy, skill))
            .Where(skill => GetPendingFragmentLevels(pendingRewards, SkillFragmentKind.External, skill.Id) <
                            config.AbsoluteSkillMaxLevel -
                            skillMaxLevelPolicy.GetExternalSkillMaxLevelWithoutRoundBonus(skill.Id))
            .ToArray();
    }

    private static IReadOnlyList<InternalSkillDefinition> ResolveInternalSkillFragmentCandidates(
        IReadOnlyList<RewardGrant> pendingRewards,
        IContentRepository contentRepository,
        GameConfig config,
        SkillMaxLevelPolicy skillMaxLevelPolicy,
        int enemyLevel)
    {
        var levelHardLimit = ResolveSkillFragmentLevelHardLimit(config, enemyLevel);
        return contentRepository.GetInternalSkills()
            .Where(skill => skill.Hard < config.CanzhangMaxHardInternalSkill)
            .Where(skill => skill.Hard < levelHardLimit)
            .Where(skill => !IsInternalSkillMaxed(config, skillMaxLevelPolicy, skill))
            .Where(skill => GetPendingFragmentLevels(pendingRewards, SkillFragmentKind.Internal, skill.Id) <
                            config.AbsoluteSkillMaxLevel -
                            skillMaxLevelPolicy.GetInternalSkillMaxLevelWithoutRoundBonus(skill.Id))
            .ToArray();
    }

    private static bool IsExternalSkillMaxed(
        GameConfig config,
        SkillMaxLevelPolicy skillMaxLevelPolicy,
        ExternalSkillDefinition skill) =>
        skillMaxLevelPolicy.GetExternalSkillMaxLevelWithoutRoundBonus(skill.Id) >= config.AbsoluteSkillMaxLevel;

    private static bool IsInternalSkillMaxed(
        GameConfig config,
        SkillMaxLevelPolicy skillMaxLevelPolicy,
        InternalSkillDefinition skill) =>
        skillMaxLevelPolicy.GetInternalSkillMaxLevelWithoutRoundBonus(skill.Id) >= config.AbsoluteSkillMaxLevel;

    private static double ResolveSkillFragmentLevelHardLimit(GameConfig config, int enemyLevel) =>
        enemyLevel >= config.MaxLevel
            ? double.PositiveInfinity
            : 2d + enemyLevel / 3d;

    private static SkillMaxLevelRewardGrant CreateExternalSkillFragmentDrop(ExternalSkillDefinition skill) =>
        new(SkillFragmentKind.External, skill.Id, $"{skill.Name}残章");

    private static SkillMaxLevelRewardGrant CreateInternalSkillFragmentDrop(InternalSkillDefinition skill) =>
        new(SkillFragmentKind.Internal, skill.Id, $"{skill.Name}残章");

    private static int GetPendingFragmentLevels(
        IReadOnlyList<RewardGrant> rewards,
        SkillFragmentKind kind,
        string skillId) =>
        rewards
            .OfType<SkillMaxLevelRewardGrant>()
            .Where(reward => reward.Kind == kind && string.Equals(reward.SkillId, skillId, StringComparison.Ordinal))
            .Sum(static reward => reward.Levels);

    public static IReadOnlyList<GeneratedEquipmentAffixRoll> GenerateEquipmentRolls(
        EquipmentDefinition equipment,
        IContentRepository contentRepository,
        GameConfig config,
        int round,
        IRandomService random)
    {
        ArgumentNullException.ThrowIfNull(config);
        var rollCount = WeightedRandomSelector.Select(
            config.EquipmentRandomAffixCountWeights,
            static entry => entry.Weight,
            random).Count;
        return EquipmentRandomAffixGenerator.GenerateRolls(
            equipment,
            contentRepository,
            round,
            rollCount,
            random);
    }

    private static IReadOnlyList<ItemDefinition> ResolveDropCandidates(
        IContentRepository contentRepository,
        int enemyLevel) =>
        contentRepository.GetItems()
            .Where(item => item.CanDrop && enemyLevel >= (item.Level - 1) * 5)
            .ToArray();

    private static T PickRandom<T>(IReadOnlyList<T> values)
    {
        if (values.Count == 0)
        {
            throw new InvalidOperationException("Random selection candidates cannot be empty.");
        }

        return values[Random.Shared.Next(values.Count)];
    }
}
