using Parlot;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Game.Expressions;

public sealed class ExpressionParser
{
    private static readonly IReadOnlySet<string> ReservedWords = new HashSet<string>(StringComparer.Ordinal)
    {
        "true", "false", "in", "not", "and", "or",
    };

    private static readonly Parser<ExpressionSyntax> Grammar = BuildGrammar();

    public ParsedExpression ParseExpression(string source, string sourceName = "expression")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        var context = new ParseContext(new Scanner(source), useNewLines: true);
        if (!Grammar.TryParse(context, out var root, out var error))
        {
            var position = error?.Position ?? context.Scanner.Cursor.Position;
            var span = new SourceSpan(
                position.Offset,
                position.Offset < source.Length ? 1 : 0,
                position.Line,
                position.Column);
            throw new ExpressionParseException(sourceName, span, error?.Message ?? "Invalid expression.");
        }

        return new ParsedExpression(source, sourceName, root);
    }

    public ParsedCall ParseCall(string source, string sourceName = "call")
    {
        var expression = ParseExpression(source, sourceName);
        if (expression.Root is not CallExpressionSyntax call)
        {
            throw new ExpressionParseException(sourceName, expression.Root.Span, "The root expression must be a function call.");
        }

        return new ParsedCall(source, sourceName, call);
    }

    private static Parser<ExpressionSyntax> BuildGrammar()
    {
        var expression = Deferred<ExpressionSyntax>();
        var arguments = ZeroOrOne(
            Separated(Terms.Char(','), expression),
            Array.Empty<ExpressionSyntax>());

        var identifier = Terms.Identifier(IsIdentifierStart, IsIdentifierPart)
            .When((_, span) => !ReservedWords.Contains(span.Span.ToString()));
        var argumentList = Between(Terms.Char('('), arguments, Terms.Char(')'));
        var identifierOrCall = identifier
            .And(argumentList.Optional())
            .Then<ExpressionSyntax>((context, start, end, result) =>
            {
                var name = result.Item1.Span.ToString();
                return result.Item2.HasValue
                    ? new CallExpressionSyntax(name, result.Item2.Value, Span(context, start, end))
                    : new IdentifierExpressionSyntax(name, Span(context, start, end));
            });

        var boolean = OneOf(
            Terms.Keyword("true").Then((context, start, end, _) => (ExpressionSyntax)new LiteralExpressionSyntax(
                ExpressionValue.FromBoolean(true), Span(context, start, end))),
            Terms.Keyword("false").Then((context, start, end, _) => (ExpressionSyntax)new LiteralExpressionSyntax(
                ExpressionValue.FromBoolean(false), Span(context, start, end))));
        var number = Terms.Number<double>(NumberOptions.Float)
            .WhenNotFollowedBy(Literals.Pattern(IsIdentifierPart, 1, 1))
            .When((_, value) => double.IsFinite(value))
            .Then<ExpressionSyntax>((context, start, end, value) =>
                new LiteralExpressionSyntax(ExpressionValue.FromNumber(value), Span(context, start, end)));
        var text = Terms.String(StringLiteralQuotes.SingleOrDouble)
            .Then<ExpressionSyntax>((context, start, end, value) => new LiteralExpressionSyntax(
                ExpressionValue.FromString(value.Span.ToString()), Span(context, start, end)));
        var list = Between(Terms.Char('['), arguments, Terms.Char(']'))
            .Then<ExpressionSyntax>((context, start, end, items) =>
                new ListExpressionSyntax(items, Span(context, start, end)));
        var parenthesized = Between(Terms.Char('('), expression, Terms.Char(')'));

        var primary = OneOf(boolean, number, text, list, identifierOrCall, parenthesized);
        var unary = Deferred<ExpressionSyntax>();
        var unaryOperator = OneOf(
            Symbol("!").Then(UnaryOperator.Not),
            Keyword("not").Then(UnaryOperator.Not),
            Symbol("+").Then(UnaryOperator.Plus),
            Symbol("-").Then(UnaryOperator.Negate));
        unary.Parser = unaryOperator
            .And(unary)
            .Then<ExpressionSyntax>((context, start, end, result) =>
                new UnaryExpressionSyntax(result.Item1, result.Item2, Span(context, start, end)))
            .Or(primary);

        var multiplicative = Parsers.LeftAssociative<ExpressionSyntax, bool>(unary,
            (Symbol("*"), Binary(BinaryOperator.Multiply)),
            (Symbol("/"), Binary(BinaryOperator.Divide)),
            (Symbol("%"), Binary(BinaryOperator.Modulo)));
        var additive = Parsers.LeftAssociative<ExpressionSyntax, bool>(multiplicative,
            (Symbol("+"), Binary(BinaryOperator.Add)),
            (Symbol("-"), Binary(BinaryOperator.Subtract)));
        var comparison = Parsers.LeftAssociative<ExpressionSyntax, bool>(additive,
            (Symbol("<="), Binary(BinaryOperator.LessThanOrEqual)),
            (Symbol(">="), Binary(BinaryOperator.GreaterThanOrEqual)),
            (Symbol("<"), Binary(BinaryOperator.LessThan)),
            (Symbol(">"), Binary(BinaryOperator.GreaterThan)),
            (NotIn(), Binary(BinaryOperator.NotIn)),
            (Keyword("in"), Binary(BinaryOperator.In)));
        var equality = Parsers.LeftAssociative<ExpressionSyntax, bool>(comparison,
            (Symbol("=="), Binary(BinaryOperator.Equal)),
            (Symbol("!="), Binary(BinaryOperator.NotEqual)));
        var conjunction = Parsers.LeftAssociative<ExpressionSyntax, bool>(equality,
            (Symbol("&&"), Binary(BinaryOperator.And)),
            (Keyword("and"), Binary(BinaryOperator.And)));
        var disjunction = Parsers.LeftAssociative<ExpressionSyntax, bool>(conjunction,
            (Symbol("||"), Binary(BinaryOperator.Or)),
            (Keyword("or"), Binary(BinaryOperator.Or)));

        expression.Parser = disjunction;
        return expression.Eof().WithWhiteSpaceParser(Literals.WhiteSpace(true));
    }

    private static Parser<bool> NotIn()
    {
        var symbolic = Terms.Text("!in")
            .WhenNotFollowedBy(Literals.Pattern(IsIdentifierPart, 1, 1))
            .Then(true);
        var textual = Keyword("not").And(Keyword("in")).Then(true);
        return symbolic.Or(textual);
    }

    private static Parser<bool> Symbol(string value) => Terms.Text(value).Then(true);

    private static Parser<bool> Keyword(string value) => Terms.Keyword(value).Then(true);

    private static Func<ExpressionSyntax, ExpressionSyntax, ExpressionSyntax> Binary(BinaryOperator @operator) =>
        (left, right) => new BinaryExpressionSyntax(
            @operator,
            left,
            right,
            new SourceSpan(
                left.Span.Offset,
                right.Span.EndOffset - left.Span.Offset,
                left.Span.Line,
                left.Span.Column));

    private static SourceSpan Span(ParseContext context, int start, int end)
    {
        var buffer = context.Scanner.Buffer;
        var line = 1;
        var column = 1;
        for (var index = 0; index < start; index++)
        {
            if (buffer[index] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return new SourceSpan(start, end - start, line, column);
    }

    private static bool IsIdentifierStart(char value) => value == '_' || value is >= 'a' and <= 'z';

    private static bool IsIdentifierPart(char value) =>
        value == '_' || value is >= 'a' and <= 'z' || char.IsDigit(value);
}
