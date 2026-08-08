namespace Game.Expressions;

public class ExpressionException : Exception
{
    public ExpressionException(string message) : base(message)
    {
    }

    public ExpressionException(string message, Exception innerException) : base(message, innerException)
    {
    }

    protected ExpressionException(string sourceName, SourceSpan span, string message, Exception? innerException = null)
        : base($"{sourceName}({span.Line},{span.Column}): {message}", innerException)
    {
        SourceName = sourceName;
        Span = span;
    }

    public string? SourceName { get; protected init; }
    public SourceSpan? Span { get; protected init; }

    public static ExpressionException WithLocation(
        ExpressionException exception,
        string sourceName,
        SourceSpan span) => exception.SourceName is not null
            ? exception
            : exception switch
            {
                ExpressionEvaluationException => new ExpressionEvaluationException(sourceName, span, exception.Message, exception),
                ExpressionBindingException => new ExpressionBindingException(sourceName, span, exception.Message, exception),
                _ => new ExpressionException(sourceName, span, exception.Message, exception),
            };
}

public sealed class ExpressionParseException : ExpressionException
{
    public ExpressionParseException(string sourceName, SourceSpan span, string message)
        : base(sourceName, span, message)
    {
    }

    public new string SourceName => base.SourceName!;
    public new SourceSpan Span => base.Span!.Value;
}

public sealed class ExpressionEvaluationException : ExpressionException
{
    public ExpressionEvaluationException(string message) : base(message)
    {
    }

    public ExpressionEvaluationException(string sourceName, SourceSpan span, string message, Exception? innerException = null)
        : base(sourceName, span, message, innerException)
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

    public ExpressionBindingException(string sourceName, SourceSpan span, string message, Exception? innerException = null)
        : base(sourceName, span, message, innerException)
    {
    }
}
