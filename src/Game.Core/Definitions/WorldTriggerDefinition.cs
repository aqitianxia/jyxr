namespace Game.Core.Definitions;

public sealed record WorldTriggerDefinition
{
    public required string Id { get; init; }

    [System.Text.Json.Serialization.JsonConverter(typeof(Game.Core.Serialization.ParsedCallJsonConverter))]
    public required ParsedCall Action { get; init; }

    [System.Text.Json.Serialization.JsonConverter(typeof(Game.Core.Serialization.ParsedExpressionJsonConverter))]
    public ParsedExpression? When { get; init; }

    public RepeatMode RepeatMode { get; init; } = RepeatMode.Once;

    public string? Description { get; init; }

}
