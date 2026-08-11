using Game.Core.Abstractions;
using Game.Core;
using Game.Core.Definitions;
using Game.Core.Model;

namespace Game.Application;

public sealed class MapService
{
    private readonly GameSession _session;
    private readonly GameConditionExpressionService _conditions;

    public MapService(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _conditions = new GameConditionExpressionService(session);
    }

    private GameState State => _session.State;
    private IContentRepository ContentRepository => _session.ContentRepository;

    public MapEnterResult EnterMap(string mapId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);

        _session.BattleService.RestorePartyBattleResources();

        var map = ContentRepository.GetMap(mapId);
        State.Location.ChangeMap(map.Id);
        _session.Events.Publish(new MapChangedEvent(map.Id));

        MapPosition? currentPosition = null;
        if (map.Kind == MapKind.Large)
        {
            currentPosition = State.Location.TryGetLargeMapPosition(map.Id, out var rememberedPosition)
                ? rememberedPosition
                : MapPosition.Zero;
            State.Location.SetLargeMapPosition(map.Id, currentPosition.Value);
        }

        return new MapEnterResult
        {
            Map = map,
            HeroPosition = currentPosition,
            ConsumedTimeSlots = 0,
            PendingInteraction = _session.WorldTriggerService.ResolvePendingTrigger(),
            Locations = BuildLocations(map),
        };
    }

    public MapInteractionResult InteractWithLocation((string MapId, MapLocationDefinition Location, MapEventDefinition? Event) location)
    {
        if (location.Event is null)
        {
            return new MapInteractionResult
            {
                Command = null,
            };
        }

        var movement = MoveHeroIfNeeded(location);
        var consumedTimeSlots = movement.ConsumedTimeSlots + 1;
        State.Clock.AdvanceTimeSlots(1);
        _session.Events.Publish(new ClockChangedEvent());

        return new MapInteractionResult
        {
            Command = location.Event.Action,
            Message = location.Event.Description,
            ConsumedTimeSlots = consumedTimeSlots,
            Movement = movement.Result,
            MapEventCompletionKey = location.Event.RepeatMode == RepeatMode.Once
                ? location.Event.Id
                : null,
        };
    }

    public int PreviewInteractionConsumedTimeSlots((string MapId, MapLocationDefinition Location, MapEventDefinition? Event) location)
    {
        if (location.Event is null)
        {
            return 0;
        }

        return CalculateMoveConsumedTimeSlots(location.MapId, location.Location) + 1;
    }

    private IReadOnlyList<(string MapId, MapLocationDefinition Location, MapEventDefinition? Event)> BuildLocations(MapDefinition map)
    {
        var locations = new List<(string MapId, MapLocationDefinition Location, MapEventDefinition? Event)>(map.Locations.Count);
        foreach (var location in map.Locations)
        {
            var mapEvent = FindTriggerEvent(location);
            if (mapEvent is null &&
                (map.Kind == MapKind.Small ||
                 location.HideWhenNoEvent))
            {
                continue;
            }

            locations.Add((map.Id, location, mapEvent));
        }

        return locations;
    }

    private MapEventDefinition? FindTriggerEvent(MapLocationDefinition location)
    {
        foreach (var mapEvent in location.Events)
        {
            if (mapEvent.RepeatMode == RepeatMode.Once &&
                IsOnceEventCompleted(mapEvent.Id))
            {
                continue;
            }

            if (!_conditions.Evaluate(mapEvent.When))
            {
                continue;
            }

            return mapEvent;
        }

        return null;
    }

    private (int ConsumedTimeSlots, MapMovementResult? Result) MoveHeroIfNeeded((string MapId, MapLocationDefinition Location, MapEventDefinition? Event) location)
    {
        var consumedTimeSlots = CalculateMoveConsumedTimeSlots(location.MapId, location.Location);
        if (consumedTimeSlots > 0)
        {
            State.Clock.AdvanceTimeSlots(consumedTimeSlots);
        }

        if (location.Location.Position is { } targetPosition &&
            ContentRepository.GetMap(location.MapId).Kind == MapKind.Large)
        {
            var currentPosition = State.Location.TryGetLargeMapPosition(location.MapId, out var position)
                ? position
                : MapPosition.Zero;
            State.Location.SetLargeMapPosition(location.MapId, targetPosition);
            var movement = currentPosition == targetPosition
                ? null
                : new MapMovementResult(location.MapId, currentPosition, targetPosition);
            return (consumedTimeSlots, movement);
        }

        return (consumedTimeSlots, null);
    }

    private int CalculateMoveConsumedTimeSlots(string mapId, MapLocationDefinition location)
    {
        if (location.Position is not { } targetPosition ||
            ContentRepository.GetMap(mapId).Kind != MapKind.Large)
        {
            return 0;
        }

        var currentPosition = State.Location.TryGetLargeMapPosition(mapId, out var position)
            ? position
            : MapPosition.Zero;
        return (int)(currentPosition.DistanceTo(targetPosition) / 10d);
    }

    public void CompleteInteraction(MapInteractionResult interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        if (interaction.MapEventCompletionKey is { } eventKey)
        {
            State.MapEventProgress.MarkCompleted(eventKey);
        }
    }

    private bool IsOnceEventCompleted(string eventKey) =>
        State.MapEventProgress.IsCompleted(eventKey);

}

public sealed record MapEnterResult
{
    public required MapDefinition Map { get; init; }
    public required int ConsumedTimeSlots { get; init; }
    public MapInteractionResult? PendingInteraction { get; init; }
    public IReadOnlyList<(string MapId, MapLocationDefinition Location, MapEventDefinition? Event)> Locations { get; init; } = [];
    public MapPosition? HeroPosition { get; init; }
}

public sealed record MapInteractionResult
{
    public ParsedCall? Command { get; init; }
    public int ConsumedTimeSlots { get; init; }
    public MapMovementResult? Movement { get; init; }
    public string? Message { get; init; }
    internal string? MapEventCompletionKey { get; init; }
}

public sealed record MapMovementResult(
    string MapId,
    MapPosition From,
    MapPosition To);
