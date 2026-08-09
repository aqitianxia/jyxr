using Game.Expressions;

namespace Game.Expressions.Tests;

public sealed class ExpressionEvaluatorTests
{
    private readonly ExpressionParser _parser = new();
    private readonly ExpressionEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_ComputesArithmeticComparisonsAndContains()
    {
        var functions = new ExpressionFunctionRegistryBuilder()
            .AddLibrary(new CoreExpressionFunctions())
            .Build();
        var variables = new DictionaryExpressionVariableResolver(new Dictionary<string, ExpressionValue>
        {
            ["silver"] = ExpressionValue.FromNumber(500),
        });
        var expression = _parser.ParseExpression("silver / 2 + 50 == 300 && contains(['子', '丑'], '子')");

        var result = _evaluator.Evaluate(expression, new ExpressionEnvironment(variables, functions));

        Assert.True(result.Boolean);
    }

    [Fact]
    public void Evaluate_ShortCircuitsFunctionCalls()
    {
        var probes = new ProbeFunctions();
        var functions = new ExpressionFunctionRegistryBuilder()
            .AddLibrary(probes)
            .Build();

        var result = _evaluator.Evaluate(
            _parser.ParseExpression("false && probe()"),
            new ExpressionEnvironment(new DictionaryExpressionVariableResolver(new Dictionary<string, ExpressionValue>()), functions));

        Assert.False(result.Boolean);
        Assert.False(probes.Invoked);
    }

    [Fact]
    public void Evaluate_ConditionalOnlyEvaluatesSelectedBranch()
    {
        var environment = EmptyEnvironment();

        Assert.Equal(4d, _evaluator.Evaluate(_parser.ParseExpression("true ? 4 : 1 / 0"), environment).Number);
        Assert.Equal(5d, _evaluator.Evaluate(_parser.ParseExpression("false ? 1 / 0 : 5"), environment).Number);
    }

    [Fact]
    public void Evaluate_CoreFloorReturnsTheMathematicalFloor()
    {
        var environment = new ExpressionEnvironment(
            new DictionaryExpressionVariableResolver(new Dictionary<string, ExpressionValue>()),
            new ExpressionFunctionRegistryBuilder().AddLibrary(new CoreExpressionFunctions()).Build());

        Assert.Equal(2d, _evaluator.Evaluate(_parser.ParseExpression("floor(2.9)"), environment).Number);
        Assert.Equal(-3d, _evaluator.Evaluate(_parser.ParseExpression("floor(-2.1)"), environment).Number);
    }

    [Theory]
    [InlineData("2 in [1, 2, 3]", true)]
    [InlineData("4 in [1, 2, 3]", false)]
    [InlineData("'si' in ['chen', 'si']", true)]
    [InlineData("1 in []", false)]
    [InlineData("2 not in [1, 3]", true)]
    [InlineData("2 !in [1, 2, 3]", false)]
    [InlineData("not false and true or false", true)]
    public void Evaluate_SupportsStrictInMembership(string source, bool expected)
    {
        var result = _evaluator.Evaluate(_parser.ParseExpression(source), EmptyEnvironment());

        Assert.Equal(expected, result.Boolean);
    }

    [Fact]
    public void Evaluate_InRejectsNonListAndMismatchedElementType()
    {
        var environment = EmptyEnvironment();
        Assert.Throws<ExpressionEvaluationException>(() =>
            _evaluator.Evaluate(_parser.ParseExpression("1 in 1"), environment));
        Assert.Throws<ExpressionEvaluationException>(() =>
            _evaluator.Evaluate(_parser.ParseExpression("'1' in [1]"), environment));
    }

    [Fact]
    public void Evaluate_RejectsDivisionByZeroAndMixedEquality()
    {
        var environment = EmptyEnvironment();
        Assert.Throws<ExpressionEvaluationException>(() =>
            _evaluator.Evaluate(_parser.ParseExpression("1 / 0"), environment));
        Assert.Throws<ExpressionEvaluationException>(() =>
            _evaluator.Evaluate(_parser.ParseExpression("1 == '1'"), environment));
    }

    [Fact]
    public void Evaluate_RejectsListEqualityAndReportsTheFailingNodeLocation()
    {
        var exception = Assert.Throws<ExpressionEvaluationException>(() =>
            _evaluator.Evaluate(_parser.ParseExpression("true && missing", "map.when"), EmptyEnvironment()));

        Assert.Equal("map.when", exception.SourceName);
        Assert.Equal(new SourceSpan(8, 7, 1, 9), exception.Span);
        Assert.Contains("map.when(1,9)", exception.Message);
        Assert.Throws<ExpressionEvaluationException>(() =>
            _evaluator.Evaluate(_parser.ParseExpression("[1] == [1]"), EmptyEnvironment()));
    }

    [Fact]
    public void EvaluateBoolean_ReportsLocationForNonBooleanResult()
    {
        var exception = Assert.Throws<ExpressionEvaluationException>(() =>
            _evaluator.EvaluateBoolean(
                _parser.ParseExpression("42", "tower.when"),
                EmptyEnvironment(),
                "condition"));

        Assert.Contains("tower.when(1,1)", exception.Message);
    }

    [Fact]
    public void Evaluate_RejectsHeterogeneousLists()
    {
        var exception = Assert.Throws<ExpressionEvaluationException>(() =>
            _evaluator.Evaluate(_parser.ParseExpression("[1, 'one']", "heterogeneous-list"), EmptyEnvironment()));
        Assert.Equal("heterogeneous-list", exception.SourceName);
        Assert.NotNull(exception.Span);
    }

    private static ExpressionEnvironment EmptyEnvironment() => new(
        new DictionaryExpressionVariableResolver(new Dictionary<string, ExpressionValue>()),
        new ExpressionFunctionRegistryBuilder().Build());

    private sealed class ProbeFunctions
    {
        public bool Invoked { get; private set; }

        [ExpressionFunction("probe")]
        public bool Probe()
        {
            Invoked = true;
            return true;
        }
    }
}
