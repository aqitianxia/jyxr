using System.Globalization;
using System.Text.Json.Serialization;

namespace Game.Application.Mods;

public enum ModType
{
    [JsonStringEnumMemberName("game")]
    Game,

    [JsonStringEnumMemberName("addon")]
    Addon,
}

public enum SaveImpact
{
    [JsonStringEnumMemberName("none")]
    None = 0,

    [JsonStringEnumMemberName("gameplay")]
    Gameplay = 1,

    [JsonStringEnumMemberName("structural")]
    Structural = 2,
}

public sealed record ModManifest(
    string Id,
    string Name,
    string Version,
    ModType Type,
    SaveImpact SaveImpact,
    string? Date = null,
    string? Description = null,
    string? Author = null,
    IReadOnlyList<string>? Packs = null,
    IReadOnlyList<string>? Assemblies = null,
    string? MinClientVersion = null,
    IReadOnlyList<string>? Dependencies = null)
{
    public const string FileName = "mod.json";
    public const string DataDirectoryName = "data";

    [JsonIgnore]
    public IReadOnlyList<string> ResolvedPacks => NormalizeRelativePaths(Packs);

    [JsonIgnore]
    public IReadOnlyList<string> ResolvedAssemblies => NormalizeRelativePaths(Assemblies);

    [JsonIgnore]
    public IReadOnlyList<string> ResolvedDependencies =>
        Dependencies is null
            ? []
            : Dependencies
                .Select(static id => id?.Trim())
                .Where(static id => !string.IsNullOrWhiteSpace(id))
                .Select(static id => id!)
                .ToArray();

    public void Validate()
    {
        EnsureStableId(Id, nameof(Id));
        EnsureRequired(Name, nameof(Name));
        EnsureRequired(Version, nameof(Version));
        EnsureDate(Date, nameof(Date));
        if (!Enum.IsDefined(SaveImpact))
        {
            throw new InvalidOperationException($"Mod manifest field '{nameof(SaveImpact)}' is invalid: {SaveImpact}.");
        }

        if (Dependencies is null)
        {
            throw new InvalidOperationException($"Mod manifest field '{nameof(Dependencies)}' is required.");
        }

        if (Type == ModType.Game && SaveImpact != SaveImpact.Structural)
        {
            throw new InvalidOperationException($"Game mod '{Id}' must declare structural save impact.");
        }

        _ = ResolvedPacks;
        _ = ResolvedAssemblies;
        var dependencies = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dependencyId in ResolvedDependencies)
        {
            EnsureStableId(dependencyId, nameof(Dependencies));
            if (string.Equals(dependencyId, Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Mod '{Id}' cannot depend on itself.");
            }

            if (!dependencies.Add(dependencyId))
            {
                throw new InvalidOperationException($"Mod '{Id}' declares dependency '{dependencyId}' more than once.");
            }
        }
    }

    private static IReadOnlyList<string> NormalizeRelativePaths(IReadOnlyList<string>? paths) =>
        paths is null
            ? []
            : paths.Select(path => NormalizeRelativePath(path, ""))
                .Where(path => path.Length > 0)
                .ToArray();

    private static string NormalizeRelativePath(string? path, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(path)
            ? fallback
            : path.Trim().Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(normalized) ||
            normalized.StartsWith("res://", StringComparison.Ordinal) ||
            normalized.StartsWith("user://", StringComparison.Ordinal) ||
            normalized.Split('/').Any(static part => part == ".."))
        {
            throw new InvalidOperationException($"Mod manifest path must be relative and stay inside the mod directory: {path}");
        }

        return normalized;
    }

    private static void EnsureRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Mod manifest field '{fieldName}' is required.");
        }
    }

    private static void EnsureDate(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!DateOnly.TryParseExact(
                value.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            throw new InvalidOperationException($"Mod manifest field '{fieldName}' must use yyyy-MM-dd format.");
        }
    }

    private static void EnsureStableId(string? value, string fieldName)
    {
        EnsureRequired(value, fieldName);
        if (value!.Any(static c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')))
        {
            throw new InvalidOperationException(
                $"Mod manifest field '{fieldName}' must contain only ASCII letters, digits, '-', '_' or '.'.");
        }
    }
}
