using Game.Presentation.Battle;

namespace Game.Godot.UI.Battle;

internal sealed class BattleSurrenderCoordinator(
	Func<BattleFlowStateMachine?> getFlow,
	Func<BattleFlowContext?> getContext,
	Action<Exception> reportFailure)
{
	private readonly CancellationTokenSource _cancellation = new();
	private bool _isConfirming;

	public void Request()
	{
		var flow = getFlow();
		if (flow is null || flow.IsBattleEnded ||
			getContext()?.IsSurrenderRequested == true || _isConfirming)
		{
			return;
		}

		_ = ConfirmAsync();
	}

	public void Cancel() => _cancellation.Cancel();

	private async Task ConfirmAsync()
	{
		_isConfirming = true;
		try
		{
			var confirmed = await UIRoot.Instance.ShowConfirmAsync(
				"确认投降吗？投降后本场战斗将判定为失败。",
				cancellationToken: _cancellation.Token);
			if (confirmed && getFlow() is { IsBattleEnded: false } flow &&
				getContext()?.IsSurrenderRequested != true)
			{
				await flow.DispatchAsync(new BattleUiIntent.Surrender());
			}
		}
		catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			reportFailure(exception);
		}
		finally
		{
			_isConfirming = false;
		}
	}
}
