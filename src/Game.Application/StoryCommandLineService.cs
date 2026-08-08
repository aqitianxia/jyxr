using System.Globalization;
using System.Text;

namespace Game.Application;

public sealed class StoryCommandLineService
{
    private readonly StoryCommandDispatcher _dispatcher;
    private readonly ExpressionParser _parser = new();

    public StoryCommandLineService(StoryCommandDispatcher dispatcher) =>
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

    public StoryCommandInvocation Parse(string line)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line);
        if (IsFullDslCall(line))
        {
            var call = _parser.ParseCall(line, "debug console");
            if (call.Root.Arguments.Any(argument => argument is not LiteralExpressionSyntax))
            {
                throw new InvalidOperationException("Console Parse returns literal arguments only; execute the full DSL call directly.");
            }
            return new StoryCommandInvocation(call.Root.Name, call.Root.Arguments.Cast<LiteralExpressionSyntax>().Select(x => x.Value).ToArray());
        }

        var tokens = Tokenize(line);
        if (tokens.Count == 0) throw new InvalidOperationException("请输入有效指令。");
        return new StoryCommandInvocation(tokens[0].Text, tokens.Skip(1).Select(ParseToken).ToArray());
    }

    public async ValueTask<StoryCommandInvocation> ExecuteAsync(string line, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line);
        if (IsFullDslCall(line))
        {
            var call = _parser.ParseCall(line, "debug console");
            await _dispatcher.ExecuteCallAsync(call, cancellationToken);
            return new StoryCommandInvocation(call.Root.Name, []);
        }

        var invocation = Parse(line);
        await _dispatcher.ExecuteCommandAsync(invocation.Name, invocation.Arguments, cancellationToken);
        return invocation;
    }

    private static bool IsFullDslCall(string line)
    {
        var index = 0;
        while (index < line.Length && char.IsWhiteSpace(line[index])) index++;
        if (index >= line.Length || line[index] != '_' && (line[index] < 'a' || line[index] > 'z')) return false;
        index++;
        while (index < line.Length && (line[index] == '_' || line[index] is >= 'a' and <= 'z' || char.IsDigit(line[index]))) index++;
        while (index < line.Length && char.IsWhiteSpace(line[index])) index++;
        return index < line.Length && line[index] == '(';
    }

    private static IReadOnlyList<ConsoleToken> Tokenize(string line)
    {
        var tokens = new List<ConsoleToken>();
        var current = new StringBuilder();
        char quote = '\0';
        var wasQuoted = false;
        foreach (var ch in line)
        {
            if (ch is '\'' or '"')
            {
                if (quote == '\0') { quote = ch; wasQuoted = true; continue; }
                if (quote == ch) { quote = '\0'; continue; }
            }
            if (char.IsWhiteSpace(ch) && quote == '\0') { Flush(tokens, current, ref wasQuoted); continue; }
            current.Append(ch);
        }
        if (quote != '\0') throw new InvalidOperationException("命令行引号未闭合。");
        Flush(tokens, current, ref wasQuoted);
        return tokens;
    }

    private static void Flush(List<ConsoleToken> tokens, StringBuilder current, ref bool wasQuoted)
    {
        if (current.Length == 0 && !wasQuoted) return;
        tokens.Add(new ConsoleToken(current.ToString(), wasQuoted));
        current.Clear();
        wasQuoted = false;
    }

    private static ExpressionValue ParseToken(ConsoleToken token)
    {
        if (token.WasQuoted) return ExpressionValue.FromString(token.Text);
        if (bool.TryParse(token.Text, out var boolean)) return ExpressionValue.FromBoolean(boolean);
        if (double.TryParse(token.Text, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)) return ExpressionValue.FromNumber(number);
        return ExpressionValue.FromString(token.Text);
    }

    private readonly record struct ConsoleToken(string Text, bool WasQuoted);
}

public sealed record StoryCommandInvocation(string Name, IReadOnlyList<ExpressionValue> Arguments);
