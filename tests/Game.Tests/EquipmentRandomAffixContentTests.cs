using System.Text.Json;
using Game.Content.Loading;
using Game.Core.Definitions;
using Game.Core.Serialization;
using Game.Expressions;

namespace Game.Tests;

public sealed class EquipmentRandomAffixContentTests
{
    [Fact]
    public void LoadFromPackage_RejectsUnknownRandomAffixExpressionVariable()
    {
        var package = new ContentPackage
        {
            RandomAffixTables =
            [
                new EquipmentRandomAffixTableDefinition
                {
                    Id = "invalid",
                    When = Expr("unknown_level > 0"),
                    Options =
                    [
                        new EquipmentRandomAffixOptionDefinition
                        {
                            Kind = EquipmentRandomAffixKind.Accuracy,
                            Weight = 1,
                            Ranges = [Range("1", "2")],
                        },
                    ],
                },
            ],
        };

        var exception = Assert.Throws<InvalidOperationException>(() => new JsonContentLoader().LoadFromPackage(package));

        Assert.Contains("unknown_level", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonDefinition_RejectsLegacyLevelFieldsAndNumericRangeEndpoints()
    {
        const string legacyTable =
            """
            {"id":"legacy","minItemLevel":1,"maxItemLevel":7,"when":"true","options":[]}
            """;
        const string numericRange =
            """
            {"min":1,"max":2}
            """;

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<EquipmentRandomAffixTableDefinition>(legacyTable, GameJson.Default));
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<EquipmentRandomAffixRangeDefinition>(numericRange, GameJson.Default));
    }

    private static EquipmentRandomAffixRangeDefinition Range(string min, string max) => new()
    {
        Min = Expr(min),
        Max = Expr(max),
    };

    private static ParsedExpression Expr(string source) => new ExpressionParser().ParseExpression(source);
}
