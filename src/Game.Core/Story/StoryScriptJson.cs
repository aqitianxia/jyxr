using Game.Core.Serialization;

namespace Game.Core.Story;

public static class StoryScriptJson
{
    public static StoryScript LoadFromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream, path);
    }

    public static StoryScript Load(Stream stream, string sourceName = "story")
    {
        using var document = GameJson.ParseDocument(stream);
        return new StoryScriptJsonParser(document.RootElement, sourceName).Parse();
    }

    public static StoryScript Parse(string json, string sourceName = "story")
    {
        using var document = GameJson.ParseDocument(json);
        return new StoryScriptJsonParser(document.RootElement, sourceName).Parse();
    }
}
