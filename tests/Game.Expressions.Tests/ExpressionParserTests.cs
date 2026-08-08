using Game.Expressions;

namespace Game.Expressions.Tests;

public sealed class ExpressionParserTests
{
    private readonly ExpressionParser _parser = new();

    [Fact]
    public void ParseExpression_UsesExpectedPrecedence()
    {
        var root = _parser.ParseExpression("1 + 2 * 3 >= 7 && !false").Root;

        var and = Assert.IsType<BinaryExpressionSyntax>(root);
        Assert.Equal(BinaryOperator.And, and.Operator);
        Assert.Equal(BinaryOperator.GreaterThanOrEqual, Assert.IsType<BinaryExpressionSyntax>(and.Left).Operator);
        Assert.Equal(UnaryOperator.Not, Assert.IsType<UnaryExpressionSyntax>(and.Right).Operator);
    }

    [Fact]
    public void ParseExpression_ParsesInAtComparisonPrecedence()
    {
        var root = Assert.IsType<BinaryExpressionSyntax>(
            _parser.ParseExpression("current_time_slot in ['辰', '巳'] && true").Root);

        Assert.Equal(BinaryOperator.And, root.Operator);
        Assert.Equal(BinaryOperator.In, Assert.IsType<BinaryExpressionSyntax>(root.Left).Operator);
    }

    [Theory]
    [InlineData("not false and true or false")]
    [InlineData("!false && true || false")]
    public void ParseExpression_ParsesBooleanOperatorSpellingsWithTheSamePrecedence(string source)
    {
        var root = Assert.IsType<BinaryExpressionSyntax>(_parser.ParseExpression(source).Root);

        Assert.Equal(BinaryOperator.Or, root.Operator);
        var and = Assert.IsType<BinaryExpressionSyntax>(root.Left);
        Assert.Equal(BinaryOperator.And, and.Operator);
        Assert.Equal(UnaryOperator.Not, Assert.IsType<UnaryExpressionSyntax>(and.Left).Operator);
    }

    [Theory]
    [InlineData("1 not in [2, 3]")]
    [InlineData("1 !in [2, 3]")]
    public void ParseExpression_ParsesNotInOperatorSpellings(string source)
    {
        var root = Assert.IsType<BinaryExpressionSyntax>(_parser.ParseExpression(source).Root);

        Assert.Equal(BinaryOperator.NotIn, root.Operator);
    }

    [Fact]
    public void ParseExpression_DoesNotTreatIdentifierPrefixAsInOperator()
    {
        var root = _parser.ParseExpression("index").Root;

        Assert.Equal("index", Assert.IsType<IdentifierExpressionSyntax>(root).Name);
        Assert.Throws<ExpressionParseException>(() => _parser.ParseExpression("1 inside [1]"));
        Assert.Throws<ExpressionParseException>(() => _parser.ParseExpression("1in[1]"));
        Assert.Throws<ExpressionParseException>(() => _parser.ParseExpression("in"));
        Assert.Throws<ExpressionParseException>(() => _parser.ParseExpression("1 !inside [1]"));
        Assert.Throws<ExpressionParseException>(() => _parser.ParseExpression("not"));
    }

    [Fact]
    public void ParseExpression_SupportsBothQuoteStylesAndEscapes()
    {
        var root = Assert.IsType<ListExpressionSyntax>(
            _parser.ParseExpression("['江湖\\n', \"侠客\\u4e16界\"]").Root);

        Assert.Equal("江湖\n", Assert.IsType<LiteralExpressionSyntax>(root.Items[0]).Value.Text);
        Assert.Equal("侠客世界", Assert.IsType<LiteralExpressionSyntax>(root.Items[1]).Value.Text);
    }

    [Fact]
    public void ParseCall_RejectsNonCallRoot()
    {
        Assert.Throws<ExpressionParseException>(() => _parser.ParseCall("silver >= 10", "test"));
    }

    [Fact]
    public void ParseExpression_RejectsTrailingInputWithLocation()
    {
        var exception = Assert.Throws<ExpressionParseException>(() => _parser.ParseExpression("true false", "condition"));
        Assert.Contains("condition", exception.Message);
        Assert.True(exception.Span.Offset > 0);
    }

    [Fact]
    public void ParseExpression_RejectsWhitespaceWithSourceLocation()
    {
        var exception = Assert.Throws<ExpressionParseException>(() => _parser.ParseExpression(" \r\n ", "empty-condition"));

        Assert.Equal("empty-condition", exception.SourceName);
        Assert.Equal(new SourceSpan(0, 4, 1, 1), exception.Span);
        Assert.Contains("empty-condition(1,1)", exception.Message);
    }

    [Fact]
    public void ParseExpression_PreservesNodeSourceSpansAfterLeadingWhitespace()
    {
        var root = Assert.IsType<UnaryExpressionSyntax>(_parser.ParseExpression(" \n  !false").Root);

        Assert.Equal(new SourceSpan(4, 6, 2, 3), root.Span);
        Assert.Equal(new SourceSpan(5, 5, 2, 4), root.Operand.Span);
    }

    [Fact]
    public void ParseExpression_RejectsNonFiniteNumberAsParseError()
    {
        Assert.Throws<ExpressionParseException>(() => _parser.ParseExpression("1e9999"));
    }

    [Theory]
    [InlineData("0", 0d)]
    [InlineData(".5", .5d)]
    [InlineData("1.", 1d)]
    [InlineData("6.02e2", 602d)]
    public void ParseExpression_ParsesSupportedNumberForms(string source, double expected)
    {
        var literal = Assert.IsType<LiteralExpressionSyntax>(_parser.ParseExpression(source).Root);

        Assert.Equal(expected, literal.Value.Number);
    }
}
