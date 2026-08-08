namespace Game.Expressions;

public enum ExpressionDiagnosticSeverity
{
    Warning,
    Error,
}

public sealed record ExpressionDiagnostic(
    ExpressionDiagnosticSeverity Severity,
    string Message,
    SourceSpan Span);

public readonly record struct ExpressionStaticType(
    ExpressionValueKind? Kind,
    ExpressionValueKind? ListElementKind = null)
{
    public bool IsUnknown => Kind is null;
    public static ExpressionStaticType Unknown => new(null);
}

public sealed class ExpressionAnalyzer
{
    public IReadOnlyList<ExpressionDiagnostic> Analyze(
        ExpressionSyntax expression,
        IExpressionCallCatalog functionCatalog,
        IReadOnlyDictionary<string, ExpressionValueKind>? knownVariables = null,
        ExpressionValueKind? expectedKind = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(functionCatalog);
        var diagnostics = new List<ExpressionDiagnostic>();
        var type = Infer(expression, functionCatalog, knownVariables, diagnostics);
        if (expectedKind is { } expected && type.Kind is { } actual && actual != expected)
        {
            diagnostics.Add(Error(expression, $"Expression must produce {expected}, got {actual}."));
        }

        return diagnostics;
    }

    public IReadOnlyList<ExpressionDiagnostic> AnalyzeCall(
        CallExpressionSyntax call,
        IExpressionCallCatalog allowedCatalog,
        IExpressionCallCatalog functionCatalog,
        IReadOnlyDictionary<string, ExpressionValueKind>? knownVariables = null)
    {
        var diagnostics = new List<ExpressionDiagnostic>();
        AnalyzeInvocation(call, allowedCatalog, functionCatalog, knownVariables, diagnostics, rootCall: true);
        return diagnostics;
    }

    private ExpressionStaticType Infer(
        ExpressionSyntax expression,
        IExpressionCallCatalog functions,
        IReadOnlyDictionary<string, ExpressionValueKind>? variables,
        List<ExpressionDiagnostic> diagnostics) => expression switch
    {
        LiteralExpressionSyntax literal => new(literal.Value.Kind),
        IdentifierExpressionSyntax identifier => InferIdentifier(identifier, variables),
        ListExpressionSyntax list => InferList(list, functions, variables, diagnostics),
        CallExpressionSyntax call => AnalyzeInvocation(call, functions, functions, variables, diagnostics, rootCall: false),
        UnaryExpressionSyntax unary => InferUnary(unary, functions, variables, diagnostics),
        BinaryExpressionSyntax binary => InferBinary(binary, functions, variables, diagnostics),
        _ => ExpressionStaticType.Unknown,
    };

    private static ExpressionStaticType InferIdentifier(
        IdentifierExpressionSyntax identifier,
        IReadOnlyDictionary<string, ExpressionValueKind>? variables) =>
        variables is not null && variables.TryGetValue(identifier.Name, out var kind)
            ? new ExpressionStaticType(kind)
            : ExpressionStaticType.Unknown;

    private ExpressionStaticType InferList(
        ListExpressionSyntax list,
        IExpressionCallCatalog functions,
        IReadOnlyDictionary<string, ExpressionValueKind>? variables,
        List<ExpressionDiagnostic> diagnostics)
    {
        ExpressionValueKind? elementKind = null;
        foreach (var item in list.Items)
        {
            var itemType = Infer(item, functions, variables, diagnostics);
            if (itemType.Kind is not { } current)
            {
                continue;
            }

            if (elementKind is { } expected && expected != current)
            {
                diagnostics.Add(Error(item, $"List element must be {expected}, got {current}."));
            }
            else
            {
                elementKind = current;
            }
        }

        return new ExpressionStaticType(ExpressionValueKind.List, elementKind);
    }

    private ExpressionStaticType AnalyzeInvocation(
        CallExpressionSyntax call,
        IExpressionCallCatalog allowedCatalog,
        IExpressionCallCatalog functions,
        IReadOnlyDictionary<string, ExpressionValueKind>? variables,
        List<ExpressionDiagnostic> diagnostics,
        bool rootCall)
    {
        if (!allowedCatalog.TryGetDescriptor(call.Name, out var descriptor))
        {
            diagnostics.Add(Error(call, $"Unknown or disallowed {(rootCall ? "call" : "function")} '{call.Name}'."));
            foreach (var argument in call.Arguments)
            {
                Infer(argument, functions, variables, diagnostics);
            }

            return ExpressionStaticType.Unknown;
        }

        var required = descriptor.Parameters.Count(parameter => !parameter.IsOptional && !parameter.IsVariadic);
        var variadic = descriptor.Parameters.LastOrDefault()?.IsVariadic == true;
        if (call.Arguments.Count < required || !variadic && call.Arguments.Count > descriptor.Parameters.Count)
        {
            diagnostics.Add(Error(call, $"Call '{call.Name}' received {call.Arguments.Count} arguments; its signature does not allow that count."));
        }

        for (var index = 0; index < call.Arguments.Count; index++)
        {
            var argument = call.Arguments[index];
            var type = Infer(argument, functions, variables, diagnostics);
            if (descriptor.Parameters.Count == 0)
            {
                continue;
            }
            var parameter = index < descriptor.Parameters.Count
                ? descriptor.Parameters[index]
                : descriptor.Parameters[^1];
            if (parameter.Kind is { } expected && type.Kind is { } actual && expected != actual)
            {
                diagnostics.Add(Error(argument, $"Argument '{parameter.Name}' of '{call.Name}' requires {expected}, got {actual}."));
            }
            else if (parameter.Kind == ExpressionValueKind.List &&
                parameter.ListElementKind is { } expectedElement &&
                type.ListElementKind is { } actualElement &&
                expectedElement != actualElement)
            {
                diagnostics.Add(Error(argument,
                    $"Argument '{parameter.Name}' of '{call.Name}' requires a list of {expectedElement}, got a list of {actualElement}."));
            }
        }

        return new ExpressionStaticType(descriptor.ReturnKind, descriptor.ReturnType.ListElementKind);
    }

    private ExpressionStaticType InferUnary(
        UnaryExpressionSyntax unary,
        IExpressionCallCatalog functions,
        IReadOnlyDictionary<string, ExpressionValueKind>? variables,
        List<ExpressionDiagnostic> diagnostics)
    {
        var operand = Infer(unary.Operand, functions, variables, diagnostics);
        var expected = unary.Operator == UnaryOperator.Not ? ExpressionValueKind.Boolean : ExpressionValueKind.Number;
        Require(unary.Operand, operand, expected, diagnostics);
        return new ExpressionStaticType(expected);
    }

    private ExpressionStaticType InferBinary(
        BinaryExpressionSyntax binary,
        IExpressionCallCatalog functions,
        IReadOnlyDictionary<string, ExpressionValueKind>? variables,
        List<ExpressionDiagnostic> diagnostics)
    {
        var left = Infer(binary.Left, functions, variables, diagnostics);
        var right = Infer(binary.Right, functions, variables, diagnostics);
        switch (binary.Operator)
        {
            case BinaryOperator.And:
            case BinaryOperator.Or:
                Require(binary.Left, left, ExpressionValueKind.Boolean, diagnostics);
                Require(binary.Right, right, ExpressionValueKind.Boolean, diagnostics);
                return new ExpressionStaticType(ExpressionValueKind.Boolean);
            case BinaryOperator.Equal:
            case BinaryOperator.NotEqual:
                if (left.Kind is { } leftKind && right.Kind is { } rightKind && leftKind != rightKind)
                {
                    diagnostics.Add(Error(binary, $"Equality requires matching types, got {leftKind} and {rightKind}."));
                }
                else if (left.Kind == ExpressionValueKind.List || right.Kind == ExpressionValueKind.List)
                {
                    diagnostics.Add(Error(binary, "Equality is not defined for List values."));
                }

                return new ExpressionStaticType(ExpressionValueKind.Boolean);
            case BinaryOperator.LessThan:
            case BinaryOperator.LessThanOrEqual:
            case BinaryOperator.GreaterThan:
            case BinaryOperator.GreaterThanOrEqual:
                Require(binary.Left, left, ExpressionValueKind.Number, diagnostics);
                Require(binary.Right, right, ExpressionValueKind.Number, diagnostics);
                return new ExpressionStaticType(ExpressionValueKind.Boolean);
            case BinaryOperator.In:
            case BinaryOperator.NotIn:
                Require(binary.Right, right, ExpressionValueKind.List, diagnostics);
                if (left.Kind is { } valueKind && right.ListElementKind is { } elementKind && valueKind != elementKind)
                {
                    diagnostics.Add(Error(binary.Left,
                        $"Operator 'in' requires {elementKind} on the left, got {valueKind}."));
                }

                return new ExpressionStaticType(ExpressionValueKind.Boolean);
            default:
                Require(binary.Left, left, ExpressionValueKind.Number, diagnostics);
                Require(binary.Right, right, ExpressionValueKind.Number, diagnostics);
                return new ExpressionStaticType(ExpressionValueKind.Number);
        }
    }

    private static void Require(
        ExpressionSyntax expression,
        ExpressionStaticType actual,
        ExpressionValueKind expected,
        List<ExpressionDiagnostic> diagnostics)
    {
        if (actual.Kind is { } kind && kind != expected)
        {
            diagnostics.Add(Error(expression, $"Expression requires {expected}, got {kind}."));
        }
    }

    private static ExpressionDiagnostic Error(ExpressionSyntax expression, string message) =>
        new(ExpressionDiagnosticSeverity.Error, message, expression.Span);
}
