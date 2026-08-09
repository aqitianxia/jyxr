using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Core.Story;

namespace Game.Content.Loading;

public sealed partial class JsonContentLoader
{
    private const string StoryDirectoryName = "story";
    private const string StoryFilePattern = "*.story.json";

    private static ContentPackage LoadPackageFromDirectory(string directoryPath, bool required = true)
    {
        if (!Directory.Exists(directoryPath))
        {
            if (!required)
            {
                return new ContentPackage();
            }

            throw new DirectoryNotFoundException($"Content directory '{directoryPath}' was not found.");
        }

        var packageNode = new JsonObject();
        foreach (var spec in ContentTypeCatalog.All)
        {
            packageNode[spec.PackagePropertyName] = LoadDefinitionArray(directoryPath, spec.FileName, required);
        }

        var package = packageNode.Deserialize<ContentPackage>(ContentJson)
            ?? throw new InvalidOperationException($"Unable to deserialize content directory '{directoryPath}'.");
        package.StoryScripts = LoadStoryScripts(directoryPath);
        return package;
    }

    private static JsonArray LoadDefinitionArray(string directoryPath, string fileName, bool required)
    {
        var filePath = Path.Combine(directoryPath, fileName);
        if (!File.Exists(filePath))
        {
            if (!required)
            {
                return [];
            }

            throw new FileNotFoundException($"Content file '{fileName}' was not found in '{directoryPath}'.", filePath);
        }

        var json = File.ReadAllText(filePath);
        var node = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        return node switch
        {
            JsonArray array => array,
            JsonObject definition => new JsonArray(definition),
            _ => throw new InvalidOperationException($"Content file '{fileName}' must be a JSON object or array."),
        };
    }

    private static Dictionary<string, StoryScript> LoadStoryScripts(string directoryPath)
    {
        var storyDirectoryPath = Path.Combine(directoryPath, StoryDirectoryName);
        if (!Directory.Exists(storyDirectoryPath))
        {
            return new Dictionary<string, StoryScript>(StringComparer.Ordinal);
        }

        var scripts = new Dictionary<string, StoryScript>(StringComparer.Ordinal);
        var storyPaths = Directory.GetFiles(storyDirectoryPath, StoryFilePattern, SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal);

        foreach (var storyPath in storyPaths)
        {
            var scriptId = BuildStoryScriptId(storyDirectoryPath, storyPath);
            Ensure(scripts.TryAdd(scriptId, StoryScriptJson.LoadFromFile(storyPath)),
                $"Story script '{scriptId}' is duplicated.");
        }

        return scripts;
    }

    private static string BuildStoryScriptId(string storyDirectoryPath, string storyPath)
    {
        var relativePath = Path.GetRelativePath(storyDirectoryPath, storyPath)
            .Replace('\\', '/');
        const string suffix = ".story.json";
        Ensure(relativePath.EndsWith(suffix, StringComparison.Ordinal),
            $"Story file '{relativePath}' must end with '{suffix}'.");
        return relativePath[..^suffix.Length];
    }
}
