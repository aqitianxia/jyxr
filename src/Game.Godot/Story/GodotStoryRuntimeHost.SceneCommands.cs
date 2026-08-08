using Game.Application;
using Game.Core.Model;
using Game.Godot.UI;

namespace Game.Godot.Story;

public sealed partial class GodotStoryRuntimeHost
{
	[StoryCommand("story")]
	private async ValueTask ExecuteStoryAsync(string storyId, CancellationToken cancellationToken)
	{
		var wasStoryPresentationActive = UIRoot.Instance.IsStoryPresentationActive;
		if (!wasStoryPresentationActive)
		{
			UIRoot.Instance.SetStoryPresentationActive(true);
		}

		try
		{
			if (!await StoryRunHelper.RunAsync(storyId, cancellationToken))
			{
				throw new InvalidOperationException($"Story command '{storyId}' did not complete a segment.");
			}
		}
		finally
		{
			if (!wasStoryPresentationActive && global::Godot.GodotObject.IsInstanceValid(UIRoot.Instance))
			{
				UIRoot.Instance.SetStoryPresentationActive(false);
			}
		}
	}

	[StoryCommand("map", "set_map", "tutorial")]
	private ValueTask ExecuteMapAsync(string mapId)
	{
		World.Instance.EnterMap(mapId);
		return ValueTask.CompletedTask;
	}

	[StoryCommand("shop")]
	private ValueTask ExecuteShopAsync(string shopId, CancellationToken cancellationToken) =>
		new(UIRoot.Instance.ShowShopPanelAsync(shopId, cancellationToken));

	[StoryCommand("chest", "xiangzi")]
	private ValueTask ExecuteChestAsync(CancellationToken cancellationToken) =>
		new(UIRoot.Instance.ShowChestPanelAsync(cancellationToken));

	[StoryCommand("battle")]
	private async ValueTask ExecuteBattleAsync(string battleId, CancellationToken cancellationToken)
	{
		var selected = await UIRoot.Instance.ShowCombatantSelectPanelAsync(battleId, cancellationToken);
		var isWin = await UIRoot.Instance.ShowBattleScreenAsync(
			new OrdinaryBattleRequest(battleId, selected.ToArray()),
			cancellationToken);
		if (!isWin)
		{
			GameFlow.GameOver();
		}
	}

	[StoryCommand("background")]
	private ValueTask ExecuteBackgroundAsync(string backgroundId)
	{
		World.Instance.SetBackground(backgroundId);
		return ValueTask.CompletedTask;
	}
}
