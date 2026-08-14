namespace Game.Core.Persistence;

public sealed record MapEventProgressRecord(
    IReadOnlyList<MapEventCompletionRecord>? CompletedEvents = null);

public sealed record MapEventCompletionRecord(
    string MapId,
    string LocationId,
    string EventId);
