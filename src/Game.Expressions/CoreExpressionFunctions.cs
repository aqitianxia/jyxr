namespace Game.Expressions;

public sealed class CoreExpressionFunctions
{
    [ExpressionFunction("contains")]
    public bool Contains(IReadOnlyList<ExpressionValue> list, ExpressionValue value)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (list.Count > 0 && list[0].Kind != value.Kind)
        {
            throw new ExpressionEvaluationException(
                "contains requires the searched value to match the list element type.");
        }

        return list.Contains(value);
    }
}
