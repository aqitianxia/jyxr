using System.Text.Json;

namespace Game.Core.Story;

internal sealed class StoryScriptJsonParser(JsonElement root, string sourceName = "story")
{
    private readonly ExpressionParser _expressionParser = new();

    public StoryScript Parse()
    {
        EnsureObject(root, "root");
        var version = GetRequiredInt32(root, "version");
        if (version != StoryScript.CurrentVersion)
        {
            throw new StoryRuntimeException(
                $"Unsupported story script version '{version}'. Expected version {StoryScript.CurrentVersion}.");
        }

        var segmentsElement = GetRequiredProperty(root, "segments");
        EnsureArray(segmentsElement, "segments");
        return new StoryScript(version, segmentsElement.EnumerateArray().Select(ParseSegment).ToArray());
    }

    private Segment ParseSegment(JsonElement element)
    {
        EnsureObject(element, "segment");
        var name = GetRequiredString(element, "name");
        return new Segment(name, ParseSteps(GetRequiredProperty(element, "steps"), $"segment '{name}'.steps"));
    }

    private IReadOnlyList<Step> ParseSteps(JsonElement element, string path)
    {
        EnsureArray(element, path);
        return element.EnumerateArray().Select(ParseStep).ToArray();
    }

    private Step ParseStep(JsonElement element)
    {
        EnsureObject(element, "step");
        var kind = GetRequiredString(element, "kind");
        return kind switch
        {
            "dialogue" => new DialogueStep(GetRequiredString(element, "speaker"), GetRequiredString(element, "text")),
            "command" => ParseCommandStep(element),
            "jump" => new JumpStep(GetRequiredString(element, "target")),
            "call" => new CallStep(GetRequiredString(element, "target")),
            "return" => new ReturnStep(),
            "choice" => ParseChoiceStep(element),
            "battle" => ParseBattleStep(element),
            "branch" => ParseBranchStep(element),
            _ => throw new StoryRuntimeException($"Unsupported step kind '{kind}'."),
        };
    }

    private CommandStep ParseCommandStep(JsonElement element)
    {
        if (TryGetProperty(element, "name", out _) || TryGetProperty(element, "args", out _))
        {
            throw new StoryRuntimeException("Story v3 command steps only accept the string 'call' form; 'name/args' are not supported.");
        }

        return new CommandStep(ParseCall(GetRequiredString(element, "call"), "command.call"));
    }

    private ChoiceStep ParseChoiceStep(JsonElement element)
    {
        var promptElement = GetRequiredProperty(element, "prompt");
        EnsureObject(promptElement, "choice.prompt");
        var prompt = new ChoicePrompt(GetRequiredString(promptElement, "speaker"), GetRequiredString(promptElement, "text"));
        var groupsElement = GetRequiredProperty(element, "groups");
        EnsureArray(groupsElement, "choice.groups");
        var groups = new List<ChoiceGroup>();
        foreach (var groupElement in groupsElement.EnumerateArray())
        {
            EnsureObject(groupElement, "choice.group");
            ParsedExpression? when = null;
            if (TryGetProperty(groupElement, "when", out var whenElement))
            {
                if (whenElement.ValueKind != JsonValueKind.String)
                {
                    throw new StoryRuntimeException("choice.group.when must be a string or be omitted.");
                }

                when = ParseExpression(whenElement.GetString() ?? string.Empty, "choice.group.when");
            }

            var optionsElement = GetRequiredProperty(groupElement, "options");
            EnsureArray(optionsElement, "choice.group.options");
            var options = optionsElement.EnumerateArray().Select(optionElement =>
            {
                EnsureObject(optionElement, "choice.option");
                return new ChoiceOption(
                    GetRequiredString(optionElement, "text"),
                    ParseSteps(GetRequiredProperty(optionElement, "steps"), "choice.option.steps"));
            }).ToArray();
            if (options.Length == 0)
            {
                throw new StoryRuntimeException("choice.group.options must contain at least one option.");
            }

            groups.Add(new ChoiceGroup(when, options));
        }

        if (groups.Count == 0)
        {
            throw new StoryRuntimeException("choice.groups must contain at least one group.");
        }

        return new ChoiceStep(prompt, groups, ParseChoiceStyle(element));
    }

    private static ChoiceStyle ParseChoiceStyle(JsonElement element)
    {
        if (!TryGetProperty(element, "style", out var styleElement))
        {
            return ChoiceStyle.Regular;
        }

        if (styleElement.ValueKind != JsonValueKind.String)
        {
            throw new StoryRuntimeException("choice.style must be a string.");
        }

        return styleElement.GetString() switch
        {
            "regular" => ChoiceStyle.Regular,
            "bold" => ChoiceStyle.Bold,
            var style => throw new StoryRuntimeException($"Unsupported choice style '{style}'."),
        };
    }

    private BattleStep ParseBattleStep(JsonElement element)
    {
        var battleId = GetRequiredString(element, "battleId");
        var outcomesElement = GetRequiredProperty(element, "outcomes");
        EnsureObject(outcomesElement, "battle.outcomes");
        var outcomes = new Dictionary<BattleOutcome, IReadOnlyList<Step>>();
        foreach (var property in outcomesElement.EnumerateObject())
        {
            outcomes.Add(ParseBattleOutcome(property.Name), ParseSteps(property.Value, $"battle.outcomes.{property.Name}"));
        }

        return new BattleStep(battleId, outcomes);
    }

    private BranchStep ParseBranchStep(JsonElement element)
    {
        var casesElement = GetRequiredProperty(element, "cases");
        EnsureArray(casesElement, "branch.cases");
        var cases = casesElement.EnumerateArray().Select(caseElement =>
        {
            EnsureObject(caseElement, "branch.case");
            return new BranchCase(
                ParseExpression(GetRequiredString(caseElement, "when"), "branch.case.when"),
                ParseSteps(GetRequiredProperty(caseElement, "steps"), "branch.case.steps"));
        }).ToArray();

        IReadOnlyList<Step>? fallback = null;
        if (TryGetProperty(element, "fallback", out var fallbackElement) && fallbackElement.ValueKind != JsonValueKind.Null)
        {
            fallback = ParseSteps(fallbackElement, "branch.fallback");
        }

        return new BranchStep(cases, fallback);
    }

    private ParsedExpression ParseExpression(string source, string path)
    {
        try
        {
            return _expressionParser.ParseExpression(source, $"{sourceName}:{path}");
        }
        catch (ExpressionException exception)
        {
            throw new StoryRuntimeException(exception.Message, exception);
        }
    }

    private ParsedCall ParseCall(string source, string path)
    {
        try
        {
            return _expressionParser.ParseCall(source, $"{sourceName}:{path}");
        }
        catch (ExpressionException exception)
        {
            throw new StoryRuntimeException(exception.Message, exception);
        }
    }

    private static BattleOutcome ParseBattleOutcome(string raw) => raw switch
    {
        "win" => BattleOutcome.Win,
        "lose" => BattleOutcome.Lose,
        "timeout" => BattleOutcome.Timeout,
        _ => throw new StoryRuntimeException($"Unsupported battle outcome '{raw}'."),
    };

    private static JsonElement GetRequiredProperty(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            throw new StoryRuntimeException($"Missing required property '{name}'.");
        }

        return value;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals(name))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string GetRequiredString(JsonElement element, string name)
    {
        var value = GetRequiredProperty(element, name);
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new StoryRuntimeException($"Property '{name}' must be a string.");
        }

        return value.GetString() ?? string.Empty;
    }

    private static int GetRequiredInt32(JsonElement element, string name)
    {
        var value = GetRequiredProperty(element, name);
        if (!value.TryGetInt32(out var result))
        {
            throw new StoryRuntimeException($"Property '{name}' must be an integer.");
        }

        return result;
    }

    private static void EnsureObject(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new StoryRuntimeException($"{path} must be a JSON object.");
        }
    }

    private static void EnsureArray(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new StoryRuntimeException($"{path} must be a JSON array.");
        }
    }
}
