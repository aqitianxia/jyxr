using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.Serialization;

public sealed class ParsedExpressionJsonConverter : JsonConverter<ParsedExpression>
{
    private static readonly ExpressionParser Parser = new();

    public override ParsedExpression Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expression values must be JSON strings.");
        }

        try
        {
            return Parser.ParseExpression(reader.GetString() ?? string.Empty, "content expression");
        }
        catch (ExpressionException exception)
        {
            throw new JsonException(exception.Message, exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, ParsedExpression value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Source);
}

public sealed class ParsedCallJsonConverter : JsonConverter<ParsedCall>
{
    private static readonly ExpressionParser Parser = new();

    public override ParsedCall Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Call values must be JSON strings.");
        }

        try
        {
            return Parser.ParseCall(reader.GetString() ?? string.Empty, "content call");
        }
        catch (ExpressionException exception)
        {
            throw new JsonException(exception.Message, exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, ParsedCall value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Source);
}
