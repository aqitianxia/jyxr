using System.Text.Json;
using Game.Core.Affix;
using Game.Core.Definitions;
using Game.Core.Serialization;

namespace Game.Tests;

public sealed class BattleContentDefinitionTests
{
    [Fact]
    public void AttackStrengthening_DefinesLegacyCompleteAttackMultiplier()
    {
        var buff = JsonSerializer.Deserialize<BuffDefinition>(
            """
            {
              "id": "attack_up",
              "name": "Attack Up",
              "isDebuff": false,
              "affixes": [
                {
                  "type": "hook",
                  "timing": "BeforeDamageCalculation",
                  "conditions": [
                    {"type": "context_unit_role", "role": "source"}
                  ],
                  "effects": [
                    {
                      "type": "modify_damage_context",
                      "field": "source_attack",
                      "op": "more",
                      "delta": 1.0,
                      "deltaPerBuffLevel": 0.1
                    }
                  ]
                }
              ]
            }
            """,
            GameJson.Default);

        Assert.NotNull(buff);

        var hook = Assert.IsType<HookAffix>(Assert.Single(buff.Affixes));
        Assert.Equal(HookTiming.BeforeDamageCalculation, hook.Timing);

        var role = Assert.IsType<ContextUnitRoleBattleHookConditionDefinition>(Assert.Single(hook.Conditions));
        Assert.Equal(BattleHookContextUnitRole.Source, role.Role);

        var effect = Assert.IsType<ModifyDamageContextBattleHookEffectDefinition>(Assert.Single(hook.Effects));
        Assert.Equal(BattleDamageContextField.SourceAttack, effect.Field);
        Assert.Equal(ModifierOp.More, effect.Op);
        Assert.Equal(1d, effect.Delta);
        Assert.Equal(0.1d, effect.DeltaPerBuffLevel);
    }
}
