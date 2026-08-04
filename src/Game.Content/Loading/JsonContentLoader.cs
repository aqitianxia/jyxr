using System.Text.Json;
using Game.Core.Definitions;
using Game.Core.Serialization;

namespace Game.Content.Loading;

public sealed record ModContentInput(
    string ModId,
    string ModDirectoryPath,
    bool Required)
{
    public string DataDirectoryPath => Path.Combine(ModDirectoryPath, "data");
    public string PatchDirectoryPath => Path.Combine(ModDirectoryPath, "patches");
}

public sealed partial class JsonContentLoader
{
    public InMemoryContentRepository LoadFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var package = JsonSerializer.Deserialize<ContentPackage>(json, GameJson.Default)
            ?? throw new InvalidOperationException("Unable to deserialize content package.");
        return LoadFromPackage(package);
    }

    public InMemoryContentRepository LoadFromDirectory(string directoryPath) =>
        LoadFromPackage(LoadPackageFromDirectory(directoryPath));

    public InMemoryContentRepository LoadFromMods(IReadOnlyList<ModContentInput> inputs)
        => LoadModContent(inputs).Repository;

    public LoadedModContent LoadModContent(IReadOnlyList<ModContentInput> inputs) =>
        ModContentLoader.Load(inputs);

    public InMemoryContentRepository LoadFromPackage(ContentPackage package)
    {
        var repository = BuildRepository(package);
        ValidateRepository(repository);
        return repository;
    }

    internal static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
