using System.Text.Json.Serialization;

namespace Game.Application.Mods;

public sealed record ModLoadout
{
    public ModLoadout(ModContext primaryMod, IReadOnlyList<ModContext> addonMods)
    {
        ArgumentNullException.ThrowIfNull(primaryMod);
        ArgumentNullException.ThrowIfNull(addonMods);
        if (primaryMod.Manifest.Type != ModType.Game)
        {
            throw new InvalidOperationException($"Primary mod '{primaryMod.ModId}' must have type 'game'.");
        }

        if (primaryMod.Manifest.SaveImpact != SaveImpact.Structural)
        {
            throw new InvalidOperationException($"Primary mod '{primaryMod.ModId}' must have structural save impact.");
        }

        if (addonMods.Any(static mod => mod.Manifest.Type != ModType.Addon))
        {
            throw new InvalidOperationException("Every non-primary mod must have type 'addon'.");
        }

        PrimaryMod = primaryMod;
        AddonMods = addonMods.ToArray();
        ModsInLoadOrder = [PrimaryMod, .. AddonMods];
        StoragePaths = primaryMod.StoragePaths;
    }

    public ModContext PrimaryMod { get; }
    public IReadOnlyList<ModContext> AddonMods { get; }
    public IReadOnlyList<ModContext> ModsInLoadOrder { get; }
    public ModStoragePaths StoragePaths { get; }

    public IReadOnlyList<ModVersionReference> CreateVersionReferences() =>
        ModsInLoadOrder
            .Select(static mod => new ModVersionReference(
                mod.ModId,
                mod.Manifest.Version,
                mod.Manifest.SaveImpact))
            .ToArray();

    public ModLoadoutComparison Compare(IReadOnlyList<ModVersionReference>? savedMods)
    {
        var current = CreateVersionReferences();
        var saved = savedMods ?? [];
        foreach (var mod in current.Concat(saved))
        {
            ValidateVersionReference(mod);
        }

        var currentById = current.ToDictionary(static mod => mod.Id, StringComparer.Ordinal);
        var savedById = saved.ToDictionary(static mod => mod.Id, StringComparer.Ordinal);
        var differences = new List<ModLoadoutDifference>();

        foreach (var currentMod in current)
        {
            if (!savedById.TryGetValue(currentMod.Id, out var savedMod))
            {
                AddDifference(currentMod.Id, ModLoadoutDifferenceKind.Added, null, currentMod.Version, currentMod.SaveImpact);
                continue;
            }

            if (!string.Equals(savedMod.Version, currentMod.Version, StringComparison.Ordinal))
            {
                AddDifference(
                    currentMod.Id,
                    ModLoadoutDifferenceKind.VersionChanged,
                    savedMod.Version,
                    currentMod.Version,
                    Max(savedMod.SaveImpact, currentMod.SaveImpact));
            }
        }

        foreach (var savedMod in saved)
        {
            if (!currentById.ContainsKey(savedMod.Id))
            {
                AddDifference(savedMod.Id, ModLoadoutDifferenceKind.Removed, savedMod.Version, null, savedMod.SaveImpact);
            }
        }

        var sharedIds = saved
            .Where(mod => currentById.TryGetValue(mod.Id, out var currentMod) &&
                          Max(mod.SaveImpact, currentMod.SaveImpact) != SaveImpact.None)
            .Select(static mod => mod.Id)
            .ToArray();
        var currentOrder = current
            .Where(mod => savedById.TryGetValue(mod.Id, out var savedMod) &&
                          Max(mod.SaveImpact, savedMod.SaveImpact) != SaveImpact.None)
            .Select(static mod => mod.Id)
            .Select((id, index) => KeyValuePair.Create(id, index))
            .ToDictionary(StringComparer.Ordinal);
        var reorderedIds = new HashSet<string>(StringComparer.Ordinal);
        for (var leftIndex = 0; leftIndex < sharedIds.Length; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < sharedIds.Length; rightIndex++)
            {
                var leftId = sharedIds[leftIndex];
                var rightId = sharedIds[rightIndex];
                if (currentOrder[leftId] <= currentOrder[rightId])
                {
                    continue;
                }

                reorderedIds.Add(leftId);
                reorderedIds.Add(rightId);
            }
        }

        foreach (var modId in reorderedIds)
        {
            var savedMod = savedById[modId];
            var currentMod = currentById[modId];
            AddDifference(
                modId,
                ModLoadoutDifferenceKind.OrderChanged,
                savedMod.Version,
                currentMod.Version,
                Max(savedMod.SaveImpact, currentMod.SaveImpact));
        }

        var warningImpact = differences.Count == 0
            ? SaveImpact.None
            : differences.Max(static difference => difference.Impact);
        return new ModLoadoutComparison(warningImpact, differences);

        void AddDifference(
            string id,
            ModLoadoutDifferenceKind kind,
            string? savedVersion,
            string? currentVersion,
            SaveImpact impact)
        {
            if (impact != SaveImpact.None)
            {
                differences.Add(new ModLoadoutDifference(id, kind, savedVersion, currentVersion, impact));
            }
        }
    }

    private static SaveImpact Max(SaveImpact left, SaveImpact right) => left >= right ? left : right;

    private static void ValidateVersionReference(ModVersionReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference.Version);
        if (!Enum.IsDefined(reference.SaveImpact))
        {
            throw new InvalidOperationException(
                $"Mod version reference '{reference.Id}' has invalid save impact '{reference.SaveImpact}'.");
        }
    }
}

public sealed record ModVersionReference(
    string Id,
    string Version,
    [property: JsonRequired] SaveImpact SaveImpact);

public enum ModLoadoutDifferenceKind
{
    Added,
    Removed,
    VersionChanged,
    OrderChanged,
}

public sealed record ModLoadoutDifference(
    string Id,
    ModLoadoutDifferenceKind Kind,
    string? SavedVersion,
    string? CurrentVersion,
    SaveImpact Impact);

public readonly record struct ModLoadoutComparison(
    SaveImpact WarningImpact,
    IReadOnlyList<ModLoadoutDifference> Differences)
{
    public bool HasWarning => WarningImpact != SaveImpact.None;
}

public sealed class ModLoadoutResolver
{
    private readonly IReadOnlyDictionary<string, ModContext> _mods;

    public ModLoadoutResolver(IEnumerable<ModContext> mods)
    {
        ArgumentNullException.ThrowIfNull(mods);
        var indexed = new Dictionary<string, ModContext>(StringComparer.Ordinal);
        foreach (var mod in mods)
        {
            if (!indexed.TryAdd(mod.ModId, mod))
            {
                throw new InvalidOperationException($"Mod id '{mod.ModId}' is duplicated.");
            }
        }

        _mods = indexed;
    }

    public ModLoadout Resolve(string primaryModId, IReadOnlyList<string> requestedAddonIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryModId);
        ArgumentNullException.ThrowIfNull(requestedAddonIds);

        var primary = GetRequired(primaryModId.Trim());
        if (primary.Manifest.Type != ModType.Game)
        {
            throw new InvalidOperationException($"Selected primary mod '{primary.ModId}' must have type 'game'.");
        }

        var requestedOrder = new List<string>();
        var requestedSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var addonId in requestedAddonIds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(addonId);
            var normalizedId = addonId.Trim();
            if (requestedSet.Add(normalizedId))
            {
                requestedOrder.Add(normalizedId);
            }
        }

        var ordered = new List<ModContext>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        foreach (var addonId in requestedOrder)
        {
            VisitAddon(addonId, primary, ordered, visited, visiting);
        }

        return new ModLoadout(primary, ordered);
    }

    public bool CanMove(IReadOnlyList<ModContext> addons, int fromIndex, int toIndex)
    {
        ArgumentNullException.ThrowIfNull(addons);
        if (fromIndex < 0 || fromIndex >= addons.Count || toIndex < 0 || toIndex >= addons.Count)
        {
            return false;
        }

        var reordered = addons.ToList();
        var moved = reordered[fromIndex];
        reordered.RemoveAt(fromIndex);
        reordered.Insert(toIndex, moved);
        var positions = reordered
            .Select((mod, index) => KeyValuePair.Create(mod.ModId, index))
            .ToDictionary(StringComparer.Ordinal);

        return reordered.All(mod => mod.Manifest.ResolvedDependencies.All(dependencyId =>
            !_mods.TryGetValue(dependencyId, out var dependency) ||
            dependency.Manifest.Type == ModType.Game ||
            positions[dependencyId] < positions[mod.ModId]));
    }

    private void VisitAddon(
        string modId,
        ModContext primary,
        ICollection<ModContext> ordered,
        ISet<string> visited,
        ISet<string> visiting)
    {
        if (visited.Contains(modId))
        {
            return;
        }

        var mod = GetRequired(modId);
        if (mod.Manifest.Type != ModType.Addon)
        {
            throw new InvalidOperationException(
                $"Addon dependency '{mod.ModId}' is a game mod. The selected primary mod is '{primary.ModId}'.");
        }

        if (!visiting.Add(modId))
        {
            throw new InvalidOperationException($"Circular mod dependency detected at '{modId}'.");
        }

        foreach (var dependencyId in mod.Manifest.ResolvedDependencies)
        {
            var dependency = GetRequired(dependencyId);
            if (dependency.Manifest.Type == ModType.Game)
            {
                if (!string.Equals(dependency.ModId, primary.ModId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Addon '{mod.ModId}' requires game mod '{dependency.ModId}', but selected primary mod is '{primary.ModId}'.");
                }

                continue;
            }

            VisitAddon(dependencyId, primary, ordered, visited, visiting);
        }

        visiting.Remove(modId);
        visited.Add(modId);
        ordered.Add(mod);
    }

    private ModContext GetRequired(string modId) =>
        _mods.TryGetValue(modId, out var mod)
            ? mod
            : throw new InvalidOperationException($"Required mod '{modId}' is not installed.");
}
