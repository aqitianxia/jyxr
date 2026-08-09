using System.Text.Json.Serialization;

namespace Game.Core.Definitions;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ItemRewardDefinition), "item")]
[JsonDerivedType(typeof(YuanbaoRewardDefinition), "yuanbao")]
[JsonDerivedType(typeof(SkillMaxLevelRewardDefinition), "skill_max_level")]
public abstract record RewardDefinition
{
    public int Quantity { get; init; } = 1;

    public string GetStableKey() =>
        this switch
        {
            ItemRewardDefinition item => $"item:{item.ItemId}",
            YuanbaoRewardDefinition => "yuanbao",
            SkillMaxLevelRewardDefinition fragment =>
                $"skill_max_level:{fragment.SkillKind}:{fragment.SkillId}",
            _ => throw new NotSupportedException($"Unsupported reward definition '{GetType().Name}'."),
        };
}

public sealed record ItemRewardDefinition : RewardDefinition
{
    public required string ItemId { get; init; }
}

public sealed record YuanbaoRewardDefinition : RewardDefinition;

public sealed record SkillMaxLevelRewardDefinition : RewardDefinition
{
    public required SkillFragmentKind SkillKind { get; init; }

    public required string SkillId { get; init; }
}

public enum SkillFragmentKind
{
    [JsonStringEnumMemberName("external")]
    External,

    [JsonStringEnumMemberName("internal")]
    Internal,
}
