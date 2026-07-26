namespace Game.Core.Battle;

public sealed record RandomBuffBattleEffectParameters(
    [property: NotWhiteSpace] string BuffId,
    [property: Probability] double Chance,
    [property: NonNegative] int MinimumLevel,
    [property: NonNegative] int MaximumLevel,
    [property: Positive] int MinimumDuration,
    [property: Positive] int MaximumDuration);

public sealed class RandomBuffBattleEffectHandler
    : CustomAbilityBattleEffectHandler<RandomBuffBattleEffectParameters>
{
    public override void Validate(RandomBuffBattleEffectParameters parameters)
    {
        if (parameters.MinimumLevel > parameters.MaximumLevel)
            throw new InvalidOperationException("Minimum buff level cannot exceed the maximum level.");
        if (parameters.MinimumDuration > parameters.MaximumDuration)
            throw new InvalidOperationException("Minimum buff duration cannot exceed the maximum duration.");
        if (parameters.MaximumLevel == int.MaxValue || parameters.MaximumDuration == int.MaxValue)
            throw new InvalidOperationException("Maximum buff level and duration must be lower than Int32.MaxValue.");
    }

    public override void Execute(
        IBattleAbilityEffectContext context,
        RandomBuffBattleEffectParameters parameters)
    {
        foreach (var target in context.Targets)
        {
            if (!Probability.RollChance(context.Random, parameters.Chance))
            {
                continue;
            }

            var level = context.Random.Next(parameters.MinimumLevel, parameters.MaximumLevel + 1);
            var duration = context.Random.Next(parameters.MinimumDuration, parameters.MaximumDuration + 1);
            context.ApplyBuff(target, parameters.BuffId, level, duration);
        }
    }
}
