using System.Text.Json;
using Game.Core.Serialization;

namespace Game.Application.Mods;

public sealed class ModRegistry
{
    private static readonly HashSet<string> AllowedManifestFields =
    [
        "id",
        "name",
        "version",
        "date",
        "description",
        "author",
        "packs",
        "assemblies",
        "minClientVersion",
        "type",
        "dependencies",
        "saveImpact",
    ];
    private static readonly string[] RequiredManifestFields =
    [
        "id",
        "name",
        "version",
        "type",
        "dependencies",
        "saveImpact",
    ];

    private readonly ProjectDataRoot _projectDataRoot;

    public ModRegistry(ProjectDataRoot projectDataRoot)
    {
        _projectDataRoot = projectDataRoot;
    }

    public IReadOnlyList<ModContext> DiscoverMods()
    {
        if (!Directory.Exists(_projectDataRoot.ModsDirectoryPath))
        {
            return [];
        }

        var mods = new List<ModContext>();
        foreach (var modDirectoryPath in Directory.EnumerateDirectories(_projectDataRoot.ModsDirectoryPath)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (TryLoadMod(modDirectoryPath, out var context))
            {
                mods.Add(context);
            }
        }

        return mods;
    }

    public ModContext LoadRequired(string modId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);
        foreach (var context in DiscoverMods())
        {
            if (string.Equals(context.ModId, modId.Trim(), StringComparison.Ordinal))
            {
                return context;
            }
        }

        throw new InvalidOperationException($"Mod '{modId}' was not found under '{_projectDataRoot.ModsDirectoryPath}'.");
    }

    public static ModContext LoadMod(ProjectDataRoot projectDataRoot, string modDirectoryPath)
    {
        var manifestPath = Path.Combine(modDirectoryPath, ModManifest.FileName);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"Mod manifest was not found: {manifestPath}", manifestPath);
        }

        var json = File.ReadAllText(manifestPath);
        ValidateManifestFields(json, manifestPath);
        var manifest = JsonSerializer.Deserialize<ModManifest>(json, GameJson.Default)
            ?? throw new InvalidOperationException($"Unable to deserialize mod manifest: {manifestPath}");
        manifest.Validate();

        var context = new ModContext(projectDataRoot, Path.GetFullPath(modDirectoryPath), manifest);
        if (manifest.Type == ModType.Game && !Directory.Exists(context.DataDirectoryPath))
        {
            throw new DirectoryNotFoundException($"Mod data directory was not found: {context.DataDirectoryPath}");
        }

        ValidatePackFiles(context);
        return context;
    }

    private bool TryLoadMod(string modDirectoryPath, out ModContext context)
    {
        try
        {
            context = LoadMod(_projectDataRoot, modDirectoryPath);
            return true;
        }
        catch
        {
            context = null!;
            return false;
        }
    }

    private static void ValidateManifestFields(string json, string manifestPath)
    {
        using var document = GameJson.ParseDocument(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Mod manifest root must be a JSON object: {manifestPath}");
        }

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!AllowedManifestFields.Contains(property.Name))
            {
                throw new InvalidOperationException(
                    $"Unsupported mod manifest field '{property.Name}' in '{manifestPath}'.");
            }
        }

        foreach (var fieldName in RequiredManifestFields)
        {
            if (!document.RootElement.TryGetProperty(fieldName, out _))
            {
                throw new InvalidOperationException(
                    $"Required mod manifest field '{fieldName}' is missing in '{manifestPath}'.");
            }
        }
    }

    private static void ValidatePackFiles(ModContext context)
    {
        foreach (var packFilePath in context.PackFilePaths)
        {
            if (!File.Exists(packFilePath))
            {
                throw new FileNotFoundException($"Mod PCK file was not found: {packFilePath}", packFilePath);
            }

            var extension = Path.GetExtension(packFilePath);
            if (!string.Equals(extension, ".pck", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Mod pack must be a .pck file: {packFilePath}");
            }
        }
    }
}
