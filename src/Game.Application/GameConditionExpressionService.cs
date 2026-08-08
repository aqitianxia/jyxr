namespace Game.Application;

internal sealed class GameConditionExpressionService
{
    private readonly ExpressionEvaluator _evaluator = new();
    private readonly GameExpressionEnvironment _environment;

    public GameConditionExpressionService(GameSession session) =>
        _environment = new GameExpressionEnvironment(session);

    public bool Evaluate(ParsedExpression? expression) =>
        expression is null || _evaluator.Evaluate(expression, _environment.Create()).AsBoolean("condition");
}
