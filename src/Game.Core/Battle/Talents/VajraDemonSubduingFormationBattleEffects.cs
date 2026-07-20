using Game.Core.Affix;

namespace Game.Core.Battle.Talents;

public sealed record VajraDemonSubduingFormationAttackSpeechParameters(
    [property: NotWhiteSpace] string Speech);

internal sealed class VajraDemonSubduingFormationAttackSpeechHandler
    : CustomBattleEffectHandler<VajraDemonSubduingFormationAttackSpeechParameters, IDamageCalculationEffectContext>
{
    public override IReadOnlySet<HookTiming> SupportedTimings { get; } =
        new HashSet<HookTiming> { HookTiming.BeforeDamageCalculation };

    public override void Execute(
        IDamageCalculationEffectContext context,
        VajraDemonSubduingFormationAttackSpeechParameters parameters)
    {
        foreach (var member in VajraDemonSubduingFormationMembers.Resolve(context))
        {
            context.RequestSpeech(member, parameters.Speech);
        }
    }
}

public sealed record VajraDemonSubduingFormationDefeatTextParameters(
    [property: NotWhiteSpace] string Text);

internal sealed class VajraDemonSubduingFormationDefeatTextHandler
    : CustomBattleEffectHandler<VajraDemonSubduingFormationDefeatTextParameters, IDefeatedEffectContext>
{
    public override IReadOnlySet<HookTiming> SupportedTimings { get; } =
        new HashSet<HookTiming> { HookTiming.OnDefeated };

    public override void Execute(
        IDefeatedEffectContext context,
        VajraDemonSubduingFormationDefeatTextParameters parameters)
    {
        foreach (var member in VajraDemonSubduingFormationMembers.Resolve(context))
        {
            context.RequestFloatText(member, parameters.Text, BattleFloatTextStyle.Special);
        }
    }
}

internal static class VajraDemonSubduingFormationMembers
{
    private const string EffectId = "金刚伏魔圈.组阵";

    public static IReadOnlyList<BattleUnit> Resolve(IBattleEffectContext context)
    {
        var instance = context.State.ScopedEffects.Instances.SingleOrDefault(value =>
            value.IsEstablished &&
            value.Definition.Id == EffectId &&
            value.Members.Contains(context.Unit.Id));
        return instance is null
            ? []
            : context.State.Units.Where(unit => instance.Members.Contains(unit.Id)).ToList();
    }
}
