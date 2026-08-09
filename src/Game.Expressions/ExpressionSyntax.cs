namespace Game.Expressions;

public readonly record struct SourceSpan(
    int Offset,
    int Length,
    int Line,
    int Column)
{
    public int EndOffset => checked(Offset + Length);
}

public abstract record ExpressionSyntax(SourceSpan Span);

public sealed record LiteralExpressionSyntax(
    ExpressionValue Value,
    SourceSpan Span) : ExpressionSyntax(Span)
{
    public LiteralExpressionSyntax(ExpressionValue value) : this(value, default) { }
}

public sealed record IdentifierExpressionSyntax(
    string Name,
    SourceSpan Span) : ExpressionSyntax(Span)
{
    public IdentifierExpressionSyntax(string name) : this(name, default) { }
}

public sealed record ListExpressionSyntax(
    IReadOnlyList<ExpressionSyntax> Items,
    SourceSpan Span) : ExpressionSyntax(Span)
{
    public ListExpressionSyntax(IReadOnlyList<ExpressionSyntax> items) : this(items, default) { }
}

public sealed record CallExpressionSyntax(
    string Name,
    IReadOnlyList<ExpressionSyntax> Arguments,
    SourceSpan Span) : ExpressionSyntax(Span)
{
    public CallExpressionSyntax(string name, IReadOnlyList<ExpressionSyntax> arguments) : this(name, arguments, default) { }
}

public enum UnaryOperator
{
    Not,
    Plus,
    Negate,
}

public sealed record UnaryExpressionSyntax(
    UnaryOperator Operator,
    ExpressionSyntax Operand,
    SourceSpan Span) : ExpressionSyntax(Span)
{
    public UnaryExpressionSyntax(UnaryOperator @operator, ExpressionSyntax operand) : this(@operator, operand, default) { }
}

public enum BinaryOperator
{
    Multiply,
    Divide,
    Modulo,
    Add,
    Subtract,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    In,
    NotIn,
    Equal,
    NotEqual,
    And,
    Or,
}

public sealed record BinaryExpressionSyntax(
    BinaryOperator Operator,
    ExpressionSyntax Left,
    ExpressionSyntax Right,
    SourceSpan Span) : ExpressionSyntax(Span)
{
    public BinaryExpressionSyntax(BinaryOperator @operator, ExpressionSyntax left, ExpressionSyntax right)
        : this(@operator, left, right, default) { }
}

public sealed record ConditionalExpressionSyntax(
    ExpressionSyntax Condition,
    ExpressionSyntax WhenTrue,
    ExpressionSyntax WhenFalse,
    SourceSpan Span) : ExpressionSyntax(Span)
{
    public ConditionalExpressionSyntax(
        ExpressionSyntax condition,
        ExpressionSyntax whenTrue,
        ExpressionSyntax whenFalse)
        : this(condition, whenTrue, whenFalse, default) { }
}

public sealed record ParsedExpression(
    string Source,
    string SourceName,
    ExpressionSyntax Root);

public sealed record ParsedCall(
    string Source,
    string SourceName,
    CallExpressionSyntax Root);
