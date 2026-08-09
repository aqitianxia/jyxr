using Game.Expressions;

namespace Game.Expressions.Tests;

public sealed class ExpressionRegistryTests
{
    [Fact]
    public void FunctionRegistry_ScansTypedOptionalAndVariadicMethods()
    {
        var registry = new ExpressionFunctionRegistryBuilder()
            .AddLibrary(new TypedFunctions())
            .Build();

        Assert.Equal(1, registry.Invoke("quantity", []).Number);
        Assert.Equal(2, registry.Invoke("count", [
            ExpressionValue.FromString("a"),
            ExpressionValue.FromString("b")]).Number);
        Assert.True(registry.Invoke("ready_alias", []).Boolean);
        Assert.Equal(2, registry.Invoke("echo", [ExpressionValue.FromList([
            ExpressionValue.FromString("a"), ExpressionValue.FromString("b")])]).List!.Count);
    }

    [Fact]
    public void Registry_BindsStrictInt32AndTypedLists()
    {
        var registry = new ExpressionCallRegistryBuilder<int>()
            .AddLibrary<TestCallAttribute>(new TypedCalls())
            .Build();

        Assert.Equal(2, registry.Invoke("string_count", [ExpressionValue.FromList([
            ExpressionValue.FromString("a"), ExpressionValue.FromString("b")])]));
        Assert.Equal(0, registry.Invoke("optional_text", []));
        Assert.Equal(3, registry.Invoke("optional_text", [ExpressionValue.FromString("abc")]));
        Assert.Throws<ExpressionBindingException>(() =>
            registry.Invoke("take", [ExpressionValue.FromNumber(1.5)]));
        Assert.Throws<ExpressionBindingException>(() =>
            registry.Invoke("string_count", [ExpressionValue.FromList([ExpressionValue.FromNumber(1)])]));
    }

    [Fact]
    public void Registry_RejectsDuplicateAliasesAndInvalidSignatures()
    {
        var builder = new ExpressionFunctionRegistryBuilder().AddLibrary(new TypedFunctions());
        Assert.Throws<InvalidOperationException>(() => builder.AddLibrary(new DuplicateFunctions()));
        Assert.Throws<InvalidOperationException>(() =>
            new ExpressionFunctionRegistryBuilder().AddLibrary(new NullableFunctions()));
        Assert.Throws<InvalidOperationException>(() =>
            new AsyncExpressionCallRegistryBuilder<int>(0).AddLibrary<TestCallAttribute>(new AsyncVoidCalls()));
    }

    [Fact]
    public void Registry_IsolatesAttributesAndUnwrapsHandlerExceptions()
    {
        var library = new MixedLibrary();
        var functions = new ExpressionFunctionRegistryBuilder().AddLibrary(library).Build();
        var calls = new ExpressionCallRegistryBuilder<int>().AddLibrary<TestCallAttribute>(library).Build();

        Assert.False(functions.TryGetDescriptor("call_only", out _));
        Assert.False(calls.TryGetDescriptor("function_only", out _));
        Assert.Throws<InvalidOperationException>(() => functions.Invoke("function_only", []));
    }

    [Fact]
    public async Task AsyncRegistry_AdaptsVoidTaskValueTaskAndResult()
    {
        var calls = new AsyncCalls();
        var registry = new AsyncExpressionCallRegistryBuilder<int>(7)
            .AddLibrary<TestCallAttribute>(calls)
            .Build();

        Assert.Equal(7, await registry.InvokeAsync("sync_void", []));
        Assert.Equal(7, await registry.InvokeAsync("task", []));
        Assert.Equal(11, await registry.InvokeAsync("result", []));
        using var source = new CancellationTokenSource();
        Assert.Equal(13, await registry.InvokeAsync("token", [], source.Token));
        Assert.Equal(source.Token, calls.SeenToken);
    }

    [Fact]
    public void Analyzer_UsesScannedDescriptorsIncludingListElementTypes()
    {
        var parser = new ExpressionParser();
        var functions = new ExpressionFunctionRegistryBuilder()
            .AddLibrary(new TypedFunctions())
            .Build();
        var analyzer = new ExpressionAnalyzer();

        var wrongType = analyzer.Analyze(parser.ParseExpression("has_item(1)").Root, functions);
        Assert.Contains(wrongType, diagnostic => diagnostic.Severity == ExpressionDiagnosticSeverity.Error);

        var wrongList = analyzer.Analyze(parser.ParseExpression("all_strings([1, 2])").Root, functions);
        Assert.Contains(wrongList, diagnostic => diagnostic.Message.Contains("list of String", StringComparison.Ordinal));

        var alias = analyzer.Analyze(parser.ParseExpression("ready_alias()").Root, functions);
        Assert.Empty(alias);

        var unknown = analyzer.Analyze(parser.ParseExpression("missing()").Root, functions);
        Assert.Contains(unknown, diagnostic => diagnostic.Message.Contains("missing", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyzer_ValidatesInContainerAndKnownElementType()
    {
        var parser = new ExpressionParser();
        var analyzer = new ExpressionAnalyzer();
        var functions = new ExpressionFunctionRegistryBuilder().Build();

        var nonList = analyzer.Analyze(parser.ParseExpression("1 in 2").Root, functions);
        Assert.Contains(nonList, diagnostic => diagnostic.Message.Contains("requires List", StringComparison.Ordinal));

        var wrongElement = analyzer.Analyze(parser.ParseExpression("'one' in [1, 2]").Root, functions);
        Assert.Contains(wrongElement, diagnostic => diagnostic.Message.Contains("requires Number", StringComparison.Ordinal));

        var listEquality = analyzer.Analyze(parser.ParseExpression("[1] == [1]").Root, functions);
        Assert.Contains(listEquality, diagnostic => diagnostic.Message.Contains("not defined for List", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyzer_ValidatesConditionalTypesAndKnownVariables()
    {
        var parser = new ExpressionParser();
        var analyzer = new ExpressionAnalyzer();
        var functions = new ExpressionFunctionRegistryBuilder().Build();
        var variables = new Dictionary<string, ExpressionValueKind>
        {
            ["enabled"] = ExpressionValueKind.Boolean,
        };

        Assert.Empty(analyzer.Analyze(
            parser.ParseExpression("enabled ? 1 : 2").Root,
            functions,
            variables,
            ExpressionValueKind.Number));
        Assert.Contains(
            analyzer.Analyze(parser.ParseExpression("1 ? 2 : 3").Root, functions, variables),
            diagnostic => diagnostic.Message.Contains("requires Boolean", StringComparison.Ordinal));
        Assert.Contains(
            analyzer.Analyze(parser.ParseExpression("enabled ? 1 : 'no'").Root, functions, variables),
            diagnostic => diagnostic.Message.Contains("matching types", StringComparison.Ordinal));
        Assert.Contains(
            analyzer.Analyze(parser.ParseExpression("missing ? 1 : 2").Root, functions, variables),
            diagnostic => diagnostic.Message.Contains("missing", StringComparison.Ordinal));
    }

    [AttributeUsage(AttributeTargets.Method)]
    private sealed class TestCallAttribute : ExpressionSymbolAttribute
    {
        public TestCallAttribute(string name, params string[] aliases) : base(name, aliases) { }
    }

    private sealed class TypedFunctions
    {
        [ExpressionFunction("quantity")]
        public int Quantity(int value = 1) => value;

        [ExpressionFunction("count")]
        public int Count(params string[] values) => values.Length;

        [ExpressionFunction("ready", "ready_alias")]
        public static bool Ready() => true;

        [ExpressionFunction("has_item")]
        public bool HasItem(string id) => id.Length > 0;

        [ExpressionFunction("all_strings")]
        public bool AllStrings(IReadOnlyList<string> values) => values.All(static value => value.Length > 0);

        [ExpressionFunction("echo")]
        public IReadOnlyList<string> Echo(IReadOnlyList<string> values) => values;

    }

    private sealed class DuplicateFunctions
    {
        [ExpressionFunction("different", "ready_alias")]
        public bool Different() => true;
    }

    private sealed class NullableFunctions
    {
        [ExpressionFunction("nullable")]
        public string? Nullable() => null;
    }

    private sealed class TypedCalls
    {
        [TestCall("take")]
        public int Take(int count) => count;

        [TestCall("string_count")]
        public int StringCount(IReadOnlyList<string> values) => values.Count;

        [TestCall("optional_text")]
        public int OptionalText(string? value = null) => value?.Length ?? 0;
    }

    private sealed class AsyncCalls
    {
        public CancellationToken SeenToken { get; private set; }

        [TestCall("sync_void")]
        public void SyncVoid() { }

        [TestCall("task")]
        public Task TaskCall() => System.Threading.Tasks.Task.CompletedTask;

        [TestCall("result")]
        public ValueTask<int> Result() => ValueTask.FromResult(11);

        [TestCall("token")]
        public async Task<int> Token(CancellationToken cancellationToken)
        {
            SeenToken = cancellationToken;
            await System.Threading.Tasks.Task.Yield();
            return 13;
        }
    }

    private sealed class AsyncVoidCalls
    {
        [TestCall("bad")]
        public async void Bad() => await System.Threading.Tasks.Task.Yield();
    }

    private sealed class MixedLibrary
    {
        [ExpressionFunction("function_only")]
        public bool FunctionOnly() => throw new InvalidOperationException("unwrapped");

        [TestCall("call_only")]
        public int CallOnly() => 1;
    }
}
