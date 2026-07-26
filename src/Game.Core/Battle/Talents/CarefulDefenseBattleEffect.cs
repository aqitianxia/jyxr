using Game.Core.Affix;

namespace Game.Core.Battle.Talents;

public sealed record CarefulDefenseBattleEffectParameters(
    string Mode,
    [property: NotWhiteSpace] string CarefulTalentId,
    [property: NotWhiteSpace] string SmartTalentId,
    [property: Probability] double CarefulChance,
    [property: NonNegative] int CarefulDamageCap,
    [property: Probability] double SmartChance,
    [property: NonNegative] int SmartDamageCap,
    BattleSpeechDefinition CarefulSpeech,
    BattleSpeechDefinition SmartSpeech);

internal sealed class CarefulDefenseBattleEffectHandler
    : CustomBattleEffectHandler<CarefulDefenseBattleEffectParameters, IDamageApplicationRuntimeContext>
{
    public override IReadOnlySet<HookTiming> SupportedTimings { get; } =
        new HashSet<HookTiming> { HookTiming.BeforeDamageApplied };

    public override void Validate(CarefulDefenseBattleEffectParameters parameters)
    {
        if (parameters.Mode is not ("careful" or "smart"))
        {
            throw new InvalidOperationException("Careful defense mode must be 'careful' or 'smart'.");
        }

    }

    public override void Execute(
        IDamageApplicationRuntimeContext context,
        CarefulDefenseBattleEffectParameters parameters)
    {
        if (context.DamageAmount <= 0)
        {
            return;
        }

        var hasCareful = context.Unit.Character.HasEffectiveTalent(parameters.CarefulTalentId);
        var hasSmart = context.Unit.Character.HasEffectiveTalent(parameters.SmartTalentId);

        if (parameters.Mode == "careful")
        {
            if (hasSmart || !Probability.RollChance(context.Random, parameters.CarefulChance))
            {
                return;
            }

            ApplyCareful(context, parameters);
            return;
        }

        if (hasCareful && Probability.RollChance(context.Random, parameters.CarefulChance))
        {
            ApplyCareful(context, parameters);
            return;
        }

        if (!Probability.RollChance(context.Random, parameters.SmartChance))
        {
            return;
        }

        var overflow = Math.Max(0, context.DamageAmount - parameters.SmartDamageCap);
        context.CapDamage(parameters.SmartDamageCap);
        if (overflow > 0)
        {
            context.ApplyMpDamage(context.Unit, overflow, parameters.SmartTalentId);
        }

        TryRequestSpeech(context, parameters.SmartSpeech);
    }

    private static void ApplyCareful(
        IDamageApplicationRuntimeContext context,
        CarefulDefenseBattleEffectParameters parameters)
    {
        context.CapDamage(parameters.CarefulDamageCap);
        TryRequestSpeech(context, parameters.CarefulSpeech);
    }

    private static void TryRequestSpeech(
        IDamageApplicationRuntimeContext context,
        BattleSpeechDefinition speech)
    {
        var selectedSpeech = BattleSpeechRuntime.TryPickLine(speech, context.Random);
        if (selectedSpeech is not null)
        {
            context.RequestSpeech(context.Unit, selectedSpeech);
        }
    }
}
