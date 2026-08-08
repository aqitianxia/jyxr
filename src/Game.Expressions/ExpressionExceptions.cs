namespace Game.Expressions;

public class ExpressionException : Exception
{
    public ExpressionException(string message) : base(message)
    {
    }

    public ExpressionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class ExpressionParseException : ExpressionException
{
    public ExpressionParseException(string sourceName, SourceSpan span, string message)
        : base($"{sourceName}({span.Line},{span.Column}): {message}")
    {
        SourceName = sourceName;
        Span = span;
    }

    public string SourceName { get; }
    public SourceSpan Span { get; }
}

public sealed class ExpressionEvaluationException : ExpressionException
{
    public ExpressionEvaluationException(string message) : base(message)
    {
    }
}

public sealed class ExpressionBindingException : ExpressionException
{
    public ExpressionBindingException(string message) : base(message)
    {
    }

    public ExpressionBindingException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
