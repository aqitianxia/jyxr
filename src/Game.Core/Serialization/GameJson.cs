using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace Game.Core.Serialization;

public static class GameJson
{
    public static JsonSerializerOptions Default { get; } = CreateDefaultOptions();

    private static JsonDocumentOptions DocumentOptions { get; } = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static JsonDocument ParseDocument(string json) =>
        JsonDocument.Parse(json, DocumentOptions);

    public static JsonDocument ParseDocument(Stream stream) =>
        JsonDocument.Parse(stream, DocumentOptions);

    internal static JsonNode? ParseNode(string json) =>
        JsonNode.Parse(json, documentOptions: DocumentOptions);

    internal static JsonNode? ParseNode(Stream stream) =>
        JsonNode.Parse(stream, documentOptions: DocumentOptions);

    private static JsonSerializerOptions CreateDefaultOptions() =>
        new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            Converters = { new JsonStringEnumConverter() },
        };
}
