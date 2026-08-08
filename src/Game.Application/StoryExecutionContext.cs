namespace Game.Application;

public sealed class StoryExecutionContext
{
    public static StoryExecutionContext Empty { get; } = new();

    private readonly IReadOnlyDictionary<string, ExpressionValue> _variables;

    public StoryExecutionContext(IReadOnlyDictionary<string, ExpressionValue>? variables = null)
    {
        _variables = variables is null
            ? new Dictionary<string, ExpressionValue>(StringComparer.Ordinal)
            : new Dictionary<string, ExpressionValue>(variables, StringComparer.Ordinal);
        foreach (var name in _variables.Keys)
        {
            ExpressionSymbol.Validate(name);
        }
    }

    public IReadOnlyDictionary<string, ExpressionValue> Variables => _variables;
}
