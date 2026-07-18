using Game.Core.Affix;

namespace Game.Core.Battle.Talents;

public sealed record TeamCountAttackBonusParameters(
    [property: NonNegative] double FactorPerUnit,
    [property: NonNegative] int MaximumUnits = 10);

public sealed class TeamCountAttackBonusHandler
    : CustomBattleEffectHandler<TeamCountAttackBonusParameters, IDamageCalculationEffectContext>
{
    public override bool SupportsPreview => true;
    public override IReadOnlySet<HookTiming> SupportedTimings { get; } =
        new HashSet<HookTiming> { HookTiming.BeforeDamageCalculation };

    public override void Execute(IDamageCalculationEffectContext context, TeamCountAttackBonusParameters parameters)
    {
        if (!ReferenceEquals(context.Source, context.Unit) || context.Skill?.Power is not > 0) return;
        var count = Math.Min(parameters.MaximumUnits, context.State.GetLivingUnits().Count(unit => unit.Team == context.Unit.Team));
        context.DamageCalculation.AddModifier(
            BattleDamageContextField.FinalDamage,
            ModifierOp.More,
            1d + parameters.FactorPerUnit * count);
    }
}

public sealed record FiveElementsDamageShareParameters(
    [property: NotWhiteSpace] string TalentId,
    [property: NonNegative] int Radius = 5,
    [property: Probability] double Chance = 0.5d);

internal sealed class FiveElementsDamageShareHandler
    : CustomBattleEffectHandler<FiveElementsDamageShareParameters, IDamageApplicationRuntimeContext>
{
    public override IReadOnlySet<HookTiming> SupportedTimings { get; } =
        new HashSet<HookTiming> { HookTiming.BeforeDamageApplied };

    public override void Execute(IDamageApplicationRuntimeContext context, FiveElementsDamageShareParameters parameters)
    {
        if (context.DamageAmount <= 0 || context.Target is null) return;
        var target = context.Target;
        var participants = context.State.GetLivingUnits()
            .Where(unit => unit.Team == target.Team && unit.Id != target.Id)
            .Where(unit => unit.Position.ManhattanDistanceTo(target.Position) <= parameters.Radius)
            .Where(unit => unit.Character.HasEffectiveTalent(parameters.TalentId))
            .Where(_ => Probability.RollChance(context.Random, parameters.Chance))
            .ToList();
        if (participants.Count == 0) return;

        var share = context.DamageAmount / (participants.Count + 1);
        var targetShare = context.DamageAmount - share * participants.Count;
        foreach (var participant in participants)
        {
            context.ApplyDirectDamage(participant, share, parameters.TalentId);
            context.RequestFloatText(participant, "五行秘术！", BattleFloatTextStyle.Special);
        }
        context.CapDamage(targetShare);
    }
}
