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
        if (line.Contains('('))
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
        return new StoryCommandInvocation(tokens[0], tokens.Skip(1).Select(ParseToken).ToArray());
    }

    public async ValueTask<StoryCommandInvocation> ExecuteAsync(string line, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line);
        if (line.Contains('('))
        {
            var call = _parser.ParseCall(line, "debug console");
            await _dispatcher.ExecuteCallAsync(call, cancellationToken);
            return new StoryCommandInvocation(call.Root.Name, []);
        }

        var invocation = Parse(line);
        await _dispatcher.ExecuteCommandAsync(invocation.Name, invocation.Arguments, cancellationToken);
        return invocation;
    }

    private static IReadOnlyList<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        char quote = '\0';
        foreach (var ch in line)
        {
            if (ch is '\'' or '"')
            {
                if (quote == '\0') { quote = ch; continue; }
                if (quote == ch) { quote = '\0'; continue; }
            }
            if (char.IsWhiteSpace(ch) && quote == '\0') { Flush(tokens, current); continue; }
            current.Append(ch);
        }
        if (quote != '\0') throw new InvalidOperationException("命令行引号未闭合。");
        Flush(tokens, current);
        return tokens;
    }

    private static void Flush(List<string> tokens, StringBuilder current)
    {
        if (current.Length == 0) return;
        tokens.Add(current.ToString());
        current.Clear();
    }

    private static ExpressionValue ParseToken(string token)
    {
        if (bool.TryParse(token, out var boolean)) return ExpressionValue.FromBoolean(boolean);
        if (double.TryParse(token, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)) return ExpressionValue.FromNumber(number);
        return ExpressionValue.FromString(token);
    }
}

public sealed record StoryCommandInvocation(string Name, IReadOnlyList<ExpressionValue> Arguments);
