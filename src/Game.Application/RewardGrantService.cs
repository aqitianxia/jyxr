using Game.Core.Definitions;

namespace Game.Application;

public sealed class RewardGrantService(GameSession session)
{
    public RewardGrant Resolve(RewardDefinition definition, int multiplier = 1)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(multiplier);

        return definition switch
        {
            ItemRewardDefinition item => new StackItemRewardGrant(
                session.ContentRepository.GetItem(item.ItemId),
                checked(item.Quantity * multiplier)),
            YuanbaoRewardDefinition yuanbao => new YuanbaoRewardGrant(
                checked(yuanbao.Amount * multiplier)),
            SkillMaxLevelRewardDefinition fragment => new SkillMaxLevelRewardGrant(
                fragment.SkillKind,
                fragment.SkillId,
                $"{ResolveSkillName(fragment.SkillKind, fragment.SkillId)}残章",
                checked(fragment.Levels * multiplier)),
            _ => throw new NotSupportedException($"Unsupported reward definition '{definition.GetType().Name}'."),
        };
    }

    public int GetRemainingSkillMaxLevelBonus(SkillFragmentKind kind, string skillId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillId);
        var currentMaxLevel = kind switch
        {
            SkillFragmentKind.External => session.SkillMaxLevelPolicy.GetExternalSkillMaxLevelWithoutRoundBonus(skillId),
            SkillFragmentKind.Internal => session.SkillMaxLevelPolicy.GetInternalSkillMaxLevelWithoutRoundBonus(skillId),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
        return Math.Max(0, session.Config.AbsoluteSkillMaxLevel - currentMaxLevel);
    }

    public void Apply(RewardGrant reward)
    {
        ArgumentNullException.ThrowIfNull(reward);

        switch (reward)
        {
            case StackItemRewardGrant stack:
                session.InventoryService.AddItem(stack.Item, stack.Quantity);
                return;
            case EquipmentRewardGrant equipment:
                session.InventoryService.AddEquipmentInstance(
                    equipment.Equipment,
                    equipment.Rolls.SelectMany(static roll => roll.Affixes).ToArray());
                return;
            case YuanbaoRewardGrant yuanbao:
                session.ProfileService.AddYuanbao(yuanbao.Amount);
                return;
            case SkillMaxLevelRewardGrant fragment:
                var appliedLevels = Math.Min(
                    fragment.Levels,
                    GetRemainingSkillMaxLevelBonus(fragment.Kind, fragment.SkillId));
                if (appliedLevels <= 0)
                {
                    return;
                }

                session.ProfileService.AddSkillMaxLevelBonus(fragment.SkillId, appliedLevels);
                session.Events.Publish(new ProfileChangedEvent());
                return;
            default:
                throw new NotSupportedException($"Unsupported reward grant '{reward.GetType().Name}'.");
        }
    }

    public string GetDisplayName(RewardDefinition definition) =>
        definition switch
        {
            ItemRewardDefinition item => session.ContentRepository.GetItem(item.ItemId).Name,
            YuanbaoRewardDefinition => "元宝",
            SkillMaxLevelRewardDefinition fragment =>
                $"{ResolveSkillName(fragment.SkillKind, fragment.SkillId)}残章",
            _ => throw new NotSupportedException($"Unsupported reward definition '{definition.GetType().Name}'."),
        };

    private string ResolveSkillName(SkillFragmentKind kind, string skillId) =>
        kind switch
        {
            SkillFragmentKind.External => session.ContentRepository.GetExternalSkill(skillId).Name,
            SkillFragmentKind.Internal => session.ContentRepository.GetInternalSkill(skillId).Name,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
}
