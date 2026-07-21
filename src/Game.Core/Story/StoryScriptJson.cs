using Game.Core.Serialization;

namespace Game.Core.Story;

public static class StoryScriptJson
{
    public static StoryScript LoadFromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    public static StoryScript Load(Stream stream)
    {
        using var document = GameJson.ParseDocument(stream);
        return new StoryScriptJsonParser(document.RootElement).Parse();
    }

    public static StoryScript Parse(string json)
    {
        using var document = GameJson.ParseDocument(json);
        return new StoryScriptJsonParser(document.RootElement).Parse();
    }
}
