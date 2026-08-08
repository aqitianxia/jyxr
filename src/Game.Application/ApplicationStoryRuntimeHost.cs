using Game.Core.Story;

namespace Game.Application;

internal sealed class ApplicationStoryRuntimeHost : IStoryRuntimeContext
{
    private readonly IRuntimeHost _externalHost;
    private readonly StoryTextInterpolator _textInterpolator;

    public ApplicationStoryRuntimeHost(
        IRuntimeHost externalHost,
        StoryCommandDispatcher commandDispatcher,
        StoryTextInterpolator textInterpolator,
        ExpressionEnvironment expressionEnvironment)
    {
        _externalHost = externalHost ?? throw new ArgumentNullException(nameof(externalHost));
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        _textInterpolator = textInterpolator ?? throw new ArgumentNullException(nameof(textInterpolator));
        Commands = commandDispatcher.Registry;
        ExpressionEnvironment = expressionEnvironment ?? throw new ArgumentNullException(nameof(expressionEnvironment));
    }

    public ExpressionEnvironment ExpressionEnvironment { get; }
    public AsyncExpressionCallRegistry<StoryCommandResult> Commands { get; }

    public ValueTask DialogueAsync(DialogueContext dialogue, CancellationToken cancellationToken) =>
        _externalHost.DialogueAsync(
            new DialogueContext(_textInterpolator.Interpolate(dialogue.Speaker), _textInterpolator.Interpolate(dialogue.Text)),
            cancellationToken);

    public ValueTask<int> ChooseOptionAsync(ChoiceContext choice, CancellationToken cancellationToken) =>
        _externalHost.ChooseOptionAsync(
            new ChoiceContext(
                _textInterpolator.Interpolate(choice.PromptSpeaker),
                _textInterpolator.Interpolate(choice.PromptText),
                choice.Options.Select(option => new ChoiceOptionView(option.Index, _textInterpolator.Interpolate(option.Text))).ToArray(),
                choice.Style),
            cancellationToken);

    public ValueTask<BattleOutcome> ResolveBattleAsync(BattleContext battle, CancellationToken cancellationToken) =>
        _externalHost.ResolveBattleAsync(battle, cancellationToken);

    public ValueTask PlayEffectAsync(string effectId, CancellationToken cancellationToken) =>
        _externalHost.PlayEffectAsync(effectId, cancellationToken);

    public ValueTask GameOverAsync(CancellationToken cancellationToken) => _externalHost.GameOverAsync(cancellationToken);
}
