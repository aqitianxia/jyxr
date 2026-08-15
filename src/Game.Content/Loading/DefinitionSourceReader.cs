using System.Text.Json;
using System.Text.Json.Nodes;

namespace Game.Content.Loading;

internal sealed record DefinitionSourceEntry(string FilePath, JsonObject Definition);

internal static class DefinitionSourceReader
{
    public static IReadOnlyList<DefinitionSourceEntry> Read(
        string dataDirectoryPath,
        ContentTypeSpec spec,
        bool required)
    {
        var sourcePath = Path.Combine(dataDirectoryPath, spec.SourcePath);
        var filePaths = spec.SourceKind switch
        {
            ContentTypeSourceKind.File => GetFilePath(sourcePath, spec, dataDirectoryPath, required),
            ContentTypeSourceKind.Directory => GetDirectoryPaths(sourcePath, spec, dataDirectoryPath, required),
            _ => throw new ArgumentOutOfRangeException(nameof(spec), spec.SourceKind, "Unknown content source kind."),
        };

        var entries = new List<DefinitionSourceEntry>();
        foreach (var filePath in filePaths)
        {
            var root = ParseNode(filePath);
            if (root is JsonObject definition)
            {
                entries.Add(new DefinitionSourceEntry(filePath, definition));
                continue;
            }

            if (root is not JsonArray definitions)
            {
                throw new ContentLoadException($"Content file '{filePath}' must be a JSON object or array.");
            }

            var detachedDefinitions = new JsonObject[definitions.Count];
            for (var index = 0; index < definitions.Count; index++)
            {
                detachedDefinitions[index] = definitions[index] as JsonObject
                    ?? throw new ContentLoadException($"Every entry in '{filePath}' must be a JSON object.");
            }

            definitions.Clear();
            foreach (var detachedDefinition in detachedDefinitions)
            {
                entries.Add(new DefinitionSourceEntry(filePath, detachedDefinition));
            }
        }

        EnsureDefinitionIdsAreUnique(entries, spec.Kind);

        return entries;
    }

    private static void EnsureDefinitionIdsAreUnique(
        IReadOnlyList<DefinitionSourceEntry> entries,
        string kind)
    {
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.Definition["id"] is not JsonValue idValue ||
                !idValue.TryGetValue<string>(out var id) ||
                string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!sources.TryAdd(id, entry.FilePath))
            {
                throw new ContentLoadException(
                    $"Definition '{kind}:{id}' in '{entry.FilePath}' conflicts with the definition loaded from '{sources[id]}'.");
            }
        }
    }

    private static IReadOnlyList<string> GetFilePath(
        string filePath,
        ContentTypeSpec spec,
        string dataDirectoryPath,
        bool required)
    {
        if (File.Exists(filePath))
        {
            return [filePath];
        }

        if (required)
        {
            throw new FileNotFoundException(
                $"Content file '{spec.SourcePath}' was not found in '{dataDirectoryPath}'.",
                filePath);
        }

        return [];
    }

    private static IReadOnlyList<string> GetDirectoryPaths(
        string directoryPath,
        ContentTypeSpec spec,
        string dataDirectoryPath,
        bool required)
    {
        if (!Directory.Exists(directoryPath))
        {
            if (required)
            {
                throw new DirectoryNotFoundException(
                    $"Content directory '{spec.SourcePath}' was not found in '{dataDirectoryPath}'.");
            }

            return [];
        }

        return Directory.GetFiles(directoryPath, spec.SearchPattern, SearchOption.AllDirectories)
            .OrderBy(
                path => Path.GetRelativePath(directoryPath, path).Replace('\\', '/'),
                StringComparer.Ordinal)
            .ToArray();
    }

    private static JsonNode ParseNode(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            return JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                })
                ?? throw new ContentLoadException($"JSON file '{filePath}' is empty.");
        }
        catch (JsonException exception)
        {
            throw new ContentLoadException(
                $"Content file '{filePath}' contains invalid JSON: {exception.Message}",
                exception);
        }
    }
}
