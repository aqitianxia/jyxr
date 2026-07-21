using Game.Core.Affix;
using Game.Core.Definitions;

namespace Game.Application;

public abstract record RewardGrant;

public sealed record StackItemRewardGrant(
    ItemDefinition Item,
    int Quantity) : RewardGrant;

public sealed record EquipmentRewardGrant(
    EquipmentDefinition Equipment,
    IReadOnlyList<GeneratedEquipmentAffixRoll> Rolls) : RewardGrant;

public sealed record YuanbaoRewardGrant(
    int Amount) : RewardGrant;

public sealed record SkillMaxLevelRewardGrant(
    SkillFragmentKind Kind,
    string SkillId,
    string DisplayName,
    int Levels) : RewardGrant
{
    public SkillMaxLevelRewardGrant(
        SkillFragmentKind kind,
        string skillId,
        string displayName)
        : this(kind, skillId, displayName, 1)
    {
    }
}

public sealed record GeneratedEquipmentAffixRoll(
    string Key,
    EquipmentRandomAffixKind Kind,
    IReadOnlyList<AffixDefinition> Affixes);
