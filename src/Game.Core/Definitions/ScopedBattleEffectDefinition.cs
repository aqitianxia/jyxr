using System.Text.Json.Serialization;
using Game.Core.Affix;
using Game.Core.Battle;

namespace Game.Core.Definitions;

public enum BattleEffectActivation
{
    [JsonStringEnumMemberName("always")] Always,
    [JsonStringEnumMemberName("source_alive")] SourceAlive,
}

public enum BattleEffectLifetime
{
    [JsonStringEnumMemberName("battle")] Battle,
    [JsonStringEnumMemberName("remove_when_member_defeated")] RemoveWhenMemberDefeated,
}

public enum ScopedBattleEffectGrantMode
{
    [JsonStringEnumMemberName("per_provider")] PerProvider,
    [JsonStringEnumMemberName("per_team_group")] PerTeamGroup,
}

public sealed record ScopedBattleEffectDefinition : IAffixProvider
{
    public required string Id { get; init; }
    public required BattleUnitSelectorDefinition Scope { get; init; }
    public BattleEffectActivation Activation { get; init; } = BattleEffectActivation.Always;
    public BattleEffectLifetime Lifetime { get; init; } = BattleEffectLifetime.Battle;
    public ScopedBattleEffectGrantMode GrantMode { get; init; } = ScopedBattleEffectGrantMode.PerProvider;
    public int RequiredMembers { get; init; } = 1;
    public IReadOnlyList<AffixDefinition> Affixes { get; init; } = [];
    public ProviderKind ProviderKind => ProviderKind.ScopedEffect;
}
