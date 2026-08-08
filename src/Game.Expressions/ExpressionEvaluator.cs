namespace Game.Expressions;

public interface IExpressionVariableResolver
{
    bool TryResolve(string name, out ExpressionValue value);
}

public sealed class DictionaryExpressionVariableResolver : IExpressionVariableResolver
{
    private readonly IReadOnlyDictionary<string, ExpressionValue> _values;

    public DictionaryExpressionVariableResolver(IReadOnlyDictionary<string, ExpressionValue> values)
    {
        _values = values ?? throw new ArgumentNullException(nameof(values));
    }

    public bool TryResolve(string name, out ExpressionValue value) => _values.TryGetValue(name, out value);
}

public sealed record ExpressionEnvironment(
    IExpressionVariableResolver Variables,
    ExpressionFunctionRegistry Functions);

public sealed class ExpressionEvaluator
{
    public ExpressionValue Evaluate(ParsedExpression expression, ExpressionEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return Evaluate(expression.Root, environment);
    }

    public ExpressionValue Evaluate(ExpressionSyntax expression, ExpressionEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(environment);

        return expression switch
        {
            LiteralExpressionSyntax literal => literal.Value,
            IdentifierExpressionSyntax identifier => ResolveVariable(identifier, environment),
            ListExpressionSyntax list => EvaluateList(list, environment),
            CallExpressionSyntax call => EvaluateCall(call, environment),
            UnaryExpressionSyntax unary => EvaluateUnary(unary, environment),
            BinaryExpressionSyntax binary => EvaluateBinary(binary, environment),
            _ => throw new ExpressionEvaluationException($"Unsupported expression node '{expression.GetType().Name}'."),
        };
    }

    public IReadOnlyList<ExpressionValue> EvaluateArguments(
        CallExpressionSyntax call,
        ExpressionEnvironment environment) =>
        call.Arguments.Select(argument => Evaluate(argument, environment)).ToArray();

    private ExpressionValue ResolveVariable(IdentifierExpressionSyntax identifier, ExpressionEnvironment environment)
    {
        if (!environment.Variables.TryResolve(identifier.Name, out var value))
        {
            throw new ExpressionEvaluationException($"Unknown expression variable '{identifier.Name}'.");
        }

        return value;
    }

    private ExpressionValue EvaluateList(ListExpressionSyntax list, ExpressionEnvironment environment) =>
        ExpressionValue.FromList(list.Items.Select(item => Evaluate(item, environment)).ToArray());

    private ExpressionValue EvaluateCall(CallExpressionSyntax call, ExpressionEnvironment environment) =>
        environment.Functions.Invoke(call.Name, EvaluateArguments(call, environment));

    private ExpressionValue EvaluateUnary(UnaryExpressionSyntax unary, ExpressionEnvironment environment)
    {
        var value = Evaluate(unary.Operand, environment);
        return unary.Operator switch
        {
            UnaryOperator.Not => ExpressionValue.FromBoolean(!value.AsBoolean("Unary '!'")),
            UnaryOperator.Plus => ExpressionValue.FromNumber(value.AsNumber("Unary '+'")),
            UnaryOperator.Negate => CheckedNumber(-value.AsNumber("Unary '-'"), "Unary '-'"),
            _ => throw new ExpressionEvaluationException($"Unsupported unary operator '{unary.Operator}'."),
        };
    }

    private ExpressionValue EvaluateBinary(BinaryExpressionSyntax binary, ExpressionEnvironment environment)
    {
        if (binary.Operator == BinaryOperator.And)
        {
            var left = Evaluate(binary.Left, environment).AsBoolean("Operator '&&'");
            return ExpressionValue.FromBoolean(left && Evaluate(binary.Right, environment).AsBoolean("Operator '&&'"));
        }

        if (binary.Operator == BinaryOperator.Or)
        {
            var left = Evaluate(binary.Left, environment).AsBoolean("Operator '||'");
            return ExpressionValue.FromBoolean(left || Evaluate(binary.Right, environment).AsBoolean("Operator '||'"));
        }

        var leftValue = Evaluate(binary.Left, environment);
        var rightValue = Evaluate(binary.Right, environment);
        return binary.Operator switch
        {
            BinaryOperator.Multiply => CheckedNumber(leftValue.AsNumber("Operator '*'") * rightValue.AsNumber("Operator '*'"), "Operator '*'") ,
            BinaryOperator.Divide => Divide(leftValue, rightValue),
            BinaryOperator.Modulo => Modulo(leftValue, rightValue),
            BinaryOperator.Add => CheckedNumber(leftValue.AsNumber("Operator '+'") + rightValue.AsNumber("Operator '+'"), "Operator '+'"),
            BinaryOperator.Subtract => CheckedNumber(leftValue.AsNumber("Operator '-'") - rightValue.AsNumber("Operator '-'"), "Operator '-'"),
            BinaryOperator.LessThan => NumberComparison(leftValue, rightValue, static (left, right) => left < right, "<"),
            BinaryOperator.LessThanOrEqual => NumberComparison(leftValue, rightValue, static (left, right) => left <= right, "<="),
            BinaryOperator.GreaterThan => NumberComparison(leftValue, rightValue, static (left, right) => left > right, ">"),
            BinaryOperator.GreaterThanOrEqual => NumberComparison(leftValue, rightValue, static (left, right) => left >= right, ">="),
            BinaryOperator.In => ExpressionValue.FromBoolean(IsIn(leftValue, rightValue)),
            BinaryOperator.NotIn => ExpressionValue.FromBoolean(!IsIn(leftValue, rightValue)),
            BinaryOperator.Equal => ExpressionValue.FromBoolean(AreEqual(leftValue, rightValue)),
            BinaryOperator.NotEqual => ExpressionValue.FromBoolean(!AreEqual(leftValue, rightValue)),
            _ => throw new ExpressionEvaluationException($"Unsupported binary operator '{binary.Operator}'."),
        };
    }

    private static bool IsIn(ExpressionValue value, ExpressionValue container)
    {
        var list = container.AsList("Operator 'in' right operand");
        if (list.Count > 0 && list[0].Kind != value.Kind)
        {
            throw new ExpressionEvaluationException(
                $"Operator 'in' requires the left operand to match the list element type, got {value.Kind} and {list[0].Kind}.");
        }

        return list.Contains(value);
    }

    private static ExpressionValue Divide(ExpressionValue left, ExpressionValue right)
    {
        var divisor = right.AsNumber("Operator '/'");
        if (divisor == 0d)
        {
            throw new ExpressionEvaluationException("Division by zero.");
        }

        return CheckedNumber(left.AsNumber("Operator '/'") / divisor, "Operator '/'");
    }

    private static ExpressionValue Modulo(ExpressionValue left, ExpressionValue right)
    {
        var divisor = right.AsNumber("Operator '%'");
        if (divisor == 0d)
        {
            throw new ExpressionEvaluationException("Modulo by zero.");
        }

        return CheckedNumber(left.AsNumber("Operator '%'") % divisor, "Operator '%'");
    }

    private static ExpressionValue NumberComparison(
        ExpressionValue left,
        ExpressionValue right,
        Func<double, double, bool> comparison,
        string operatorName) =>
        ExpressionValue.FromBoolean(comparison(
            left.AsNumber($"Operator '{operatorName}'"),
            right.AsNumber($"Operator '{operatorName}'")));

    private static bool AreEqual(ExpressionValue left, ExpressionValue right)
    {
        if (left.Kind != right.Kind)
        {
            throw new ExpressionEvaluationException($"Equality requires values of the same type, got {left.Kind} and {right.Kind}.");
        }

        return left.Kind switch
        {
            ExpressionValueKind.Boolean => left.Boolean == right.Boolean,
            ExpressionValueKind.Number => left.Number.Equals(right.Number),
            ExpressionValueKind.String => string.Equals(left.Text, right.Text, StringComparison.Ordinal),
            _ => throw new ExpressionEvaluationException($"Equality is not defined for {left.Kind} values."),
        };
    }

    private static ExpressionValue CheckedNumber(double value, string context)
    {
        if (!double.IsFinite(value))
        {
            throw new ExpressionEvaluationException($"{context} produced a non-finite number.");
        }

        return ExpressionValue.FromNumber(value);
    }
}

public sealed class ExpressionCallExecutor
{
    private readonly ExpressionEvaluator _evaluator = new();

    public TResult Execute<TResult>(
        ParsedCall call,
        ExpressionEnvironment environment,
        ExpressionCallRegistry<TResult> registry) =>
        registry.Invoke(call.Root.Name, _evaluator.EvaluateArguments(call.Root, environment));

    public ValueTask<TResult> ExecuteAsync<TResult>(
        ParsedCall call,
        ExpressionEnvironment environment,
        AsyncExpressionCallRegistry<TResult> registry,
        CancellationToken cancellationToken = default) =>
        registry.InvokeAsync(call.Root.Name, _evaluator.EvaluateArguments(call.Root, environment), cancellationToken);
}
