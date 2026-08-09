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
            "set" => ParseSetVariableStep(element),
            "delete" => new DeleteVariableStep(ParseVariableName(element, "target")),
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

    private SetVariableStep ParseSetVariableStep(JsonElement element) =>
        new(
            ParseVariableName(element, "target"),
            ParseExpression(GetRequiredString(element, "value"), "set.value"));

    private static string ParseVariableName(JsonElement element, string propertyName)
    {
        var name = GetRequiredString(element, propertyName);
        try
        {
            ExpressionSymbol.Validate(name);
            return name;
        }
        catch (ArgumentException exception)
        {
            throw new StoryRuntimeException($"Invalid story variable name '{name}'.", exception);
        }
    }

    private ChoiceStep ParseChoiceStep(JsonElement element)
    {
        var promptElement = GetRequiredProperty(element, "prompt");
        EnsureObject(promptElement, "choice.prompt");
        var prompt = new ChoicePrompt(GetRequiredString(promptElement, "speaker"), GetRequiredString(promptElement, "text"));
        if (TryGetProperty(element, "groups", out _))
        {
            throw new StoryRuntimeException("Story v3 choice steps use 'blocks'; the old 'groups' shape is not supported.");
        }

        var blocksElement = GetRequiredProperty(element, "blocks");
        EnsureArray(blocksElement, "choice.blocks");
        var blocks = blocksElement.EnumerateArray().Select(ParseChoiceBlock).ToArray();
        if (blocks.Length == 0)
        {
            throw new StoryRuntimeException("choice.blocks must contain at least one block.");
        }

        return new ChoiceStep(prompt, blocks, ParseChoiceStyle(element));
    }

    private ChoiceBlock ParseChoiceBlock(JsonElement element)
    {
        EnsureObject(element, "choice.block");
        return GetRequiredString(element, "kind") switch
        {
            "options" => new ChoiceOptionsBlock(ParseChoiceOptions(
                GetRequiredProperty(element, "options"),
                "choice.optionsBlock.options")),
            "branch" => ParseChoiceBranchBlock(element),
            var kind => throw new StoryRuntimeException($"Unsupported choice block kind '{kind}'."),
        };
    }

    private ChoiceBranchBlock ParseChoiceBranchBlock(JsonElement element)
    {
        var casesElement = GetRequiredProperty(element, "cases");
        EnsureArray(casesElement, "choice.branch.cases");
        var cases = casesElement.EnumerateArray().Select(caseElement =>
        {
            EnsureObject(caseElement, "choice.branch.case");
            return new ChoiceBranchCase(
                ParseExpression(GetRequiredString(caseElement, "when"), "choice.branch.case.when"),
                ParseChoiceOptions(
                    GetRequiredProperty(caseElement, "options"),
                    "choice.branch.case.options"));
        }).ToArray();
        if (cases.Length == 0)
        {
            throw new StoryRuntimeException("choice.branch.cases must contain at least one case.");
        }

        IReadOnlyList<ChoiceOption>? fallback = null;
        var fallbackElement = GetRequiredProperty(element, "fallback");
        if (fallbackElement.ValueKind != JsonValueKind.Null)
        {
            fallback = ParseChoiceOptions(fallbackElement, "choice.branch.fallback");
        }

        return new ChoiceBranchBlock(cases, fallback);
    }

    private IReadOnlyList<ChoiceOption> ParseChoiceOptions(JsonElement element, string path)
    {
        EnsureArray(element, path);
        var options = element.EnumerateArray().Select(optionElement =>
        {
            EnsureObject(optionElement, "choice.option");
            ParsedExpression? when = null;
            if (TryGetProperty(optionElement, "when", out var whenElement))
            {
                if (whenElement.ValueKind != JsonValueKind.String)
                {
                    throw new StoryRuntimeException("choice.option.when must be a string or be omitted.");
                }

                when = ParseExpression(whenElement.GetString() ?? string.Empty, "choice.option.when");
            }

            return new ChoiceOption(
                GetRequiredString(optionElement, "text"),
                when,
                ParseSteps(GetRequiredProperty(optionElement, "steps"), "choice.option.steps"));
        }).ToArray();
        if (options.Length == 0)
        {
            throw new StoryRuntimeException($"{path} must contain at least one option.");
        }

        return options;
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
