using System.Text.Json.Nodes;
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
        var root = GameJson.ParseNode(stream);
        return new StoryScriptJsonParser(root, sourceName).Parse();
    }

    public static StoryScript Parse(string json, string sourceName = "story")
    {
        var root = GameJson.ParseNode(json);
        return new StoryScriptJsonParser(root, sourceName).Parse();
    }

    public static StoryScript Parse(JsonObject root, string sourceName = "story") =>
        new StoryScriptJsonParser(root, sourceName).Parse();
}
