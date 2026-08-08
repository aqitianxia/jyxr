using System.Globalization;

namespace Game.Expressions;

public enum ExpressionValueKind
{
    Boolean,
    Number,
    String,
    List,
}

public readonly record struct ExpressionValue
{
    private ExpressionValue(
        ExpressionValueKind kind,
        bool boolean,
        double number,
        string? text,
        IReadOnlyList<ExpressionValue>? list)
    {
        Kind = kind;
        Boolean = boolean;
        Number = number;
        Text = text;
        List = list;
    }

    public ExpressionValueKind Kind { get; }
    public bool Boolean { get; }
    public double Number { get; }
    public string? Text { get; }
    public IReadOnlyList<ExpressionValue>? List { get; }

    public static ExpressionValue FromBoolean(bool value) =>
        new(ExpressionValueKind.Boolean, value, default, null, null);

    public static ExpressionValue FromNumber(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Expression numbers must be finite.");
        }

        return new ExpressionValue(ExpressionValueKind.Number, default, value, null, null);
    }

    public static ExpressionValue FromString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ExpressionValue(ExpressionValueKind.String, default, default, value, null);
    }

    public static ExpressionValue FromList(IReadOnlyList<ExpressionValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var copy = values.ToArray();
        if (copy.Length > 1 && copy.Any(value => value.Kind != copy[0].Kind))
        {
            throw new ArgumentException("Expression lists must contain values of one type.", nameof(values));
        }

        return new ExpressionValue(ExpressionValueKind.List, default, default, null, copy);
    }

    public bool AsBoolean(string context) => Kind == ExpressionValueKind.Boolean
        ? Boolean
        : throw TypeError(context, ExpressionValueKind.Boolean);

    public double AsNumber(string context) => Kind == ExpressionValueKind.Number
        ? Number
        : throw TypeError(context, ExpressionValueKind.Number);

    public int AsInt32(string context)
    {
        var number = AsNumber(context);
        if (number % 1d != 0d || number is < int.MinValue or > int.MaxValue)
        {
            throw new ExpressionEvaluationException($"{context} requires a 32-bit integer.");
        }

        return (int)number;
    }

    public string AsString(string context) => Kind == ExpressionValueKind.String && Text is not null
        ? Text
        : throw TypeError(context, ExpressionValueKind.String);

    public IReadOnlyList<ExpressionValue> AsList(string context) => Kind == ExpressionValueKind.List && List is not null
        ? List
        : throw TypeError(context, ExpressionValueKind.List);

    public override string ToString() => Kind switch
    {
        ExpressionValueKind.Boolean => Boolean ? "true" : "false",
        ExpressionValueKind.Number => Number.ToString(CultureInfo.InvariantCulture),
        ExpressionValueKind.String => Text ?? string.Empty,
        ExpressionValueKind.List => $"[{string.Join(", ", List ?? [])}]",
        _ => string.Empty,
    };

    private ExpressionEvaluationException TypeError(string context, ExpressionValueKind expected) =>
        new($"{context} requires {expected}, got {Kind}.");
}
