namespace Game.Application;

public sealed record OrdinaryBattleVictorySettlement(
    int ExperiencePerMember,
    int Silver,
    IReadOnlyList<RewardGrant> Rewards);
