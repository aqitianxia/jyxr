namespace Game.Application;

internal sealed class StoryVariableMutationService
{
    private readonly GameSession _session;
    private readonly IReadOnlySet<string> _contextVariableNames;

    public StoryVariableMutationService(GameSession session, StoryExecutionContext context)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentNullException.ThrowIfNull(context);
        _contextVariableNames = context.Variables.Keys.ToHashSet(StringComparer.Ordinal);
    }

    public void Assign(string name, ExpressionValue value)
    {
        EnsureWritable(name);
        _session.State.Story.SetVariable(name, value);
        _session.Events.Publish(new StoryStateChangedEvent());
    }

    public bool Delete(string name, string operationName)
    {
        EnsureWritable(name);
        if (!_session.State.Story.RemoveVariable(name))
        {
            _session.DiagnosticLogger.Warning(
                $"Operation '{operationName}' ignored missing story variable '{name}'.");
            return false;
        }

        _session.Events.Publish(new StoryStateChangedEvent());
        return true;
    }

    private void EnsureWritable(string name)
    {
        ExpressionSymbol.Validate(name);
        if (GameExpressionSymbols.BuiltInVariables.Contains(name) || _contextVariableNames.Contains(name))
        {
            throw new InvalidOperationException($"'{name}' is a read-only expression variable.");
        }
    }
}
