using Game.Core;
using Game.Core.Definitions;
using Game.Core.Model;

namespace Game.Application;

public sealed class WorldTriggerService
{
    private readonly GameSession _session;
    private readonly GameConditionExpressionService _conditions;

    public WorldTriggerService(GameSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _conditions = new GameConditionExpressionService(session);
    }

    private GameState State => _session.State;

    public MapInteractionResult? ResolvePendingTrigger()
    {
        if (State.WorldTriggers.IsBlocked)
        {
            return null;
        }

        foreach (var trigger in _session.ContentRepository.GetWorldTriggers())
        {
            if (IsCompleted(trigger) || !_conditions.Evaluate(trigger.When))
            {
                continue;
            }

            // Mark before dispatch so a map-changing command cannot resolve the same trigger recursively.
            if (trigger.RepeatMode == RepeatMode.Once)
            {
                State.WorldTriggers.MarkCompleted(trigger.Id);
            }

            return new MapInteractionResult
            {
                Command = trigger.Action,
                Message = trigger.Description,
                ConsumedTimeSlots = 0,
            };
        }

        return null;
    }

    public void Block() => State.WorldTriggers.Block();

    public void Unblock() => State.WorldTriggers.Unblock();

    private bool IsCompleted(WorldTriggerDefinition trigger) =>
        trigger.RepeatMode == RepeatMode.Once && State.WorldTriggers.IsCompleted(trigger.Id);
}
