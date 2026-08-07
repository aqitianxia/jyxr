using Game.Application;
using Game.Core.Model;
using Game.Core.Story;
using Game.Godot.Map;
using Game.Godot.UI;
using Godot;

namespace Game.Godot.Story;

public sealed partial class GodotStoryRuntimeHost
{
	[StoryCommand("debug_story", "dstory")]
	private ValueTask ExecuteDebugStoryAsync(string storyId)
	{
		Game.ContentRepository.GetStorySegment(storyId);
		Callable.From(() =>
		{
			_ = RunDebugStoryAsync(storyId);
		}).CallDeferred();
		return ValueTask.CompletedTask;
	}

	private static async Task RunDebugStoryAsync(string storyId)
	{
		UIRoot.Instance.ClosePanel();
		UIRoot.Instance.SetStoryPresentationActive(true);
		try
		{
			await Game.StoryService.ExecuteAsync(storyId);
		}
		catch (Exception exception)
		{
			Game.Logger.Error($"Debug story failed: {storyId}", exception);
			UIRoot.Instance.ShowToast($"测试剧情执行失败：{exception.Message}");
		}
		finally
		{
			if (GodotObject.IsInstanceValid(UIRoot.Instance))
			{
				UIRoot.Instance.SetStoryPresentationActive(false);
			}

			if (GodotObject.IsInstanceValid(World.Instance) && World.Instance.CurrentScene is MapScreen)
			{
				World.Instance.RefreshCurrentMap();
			}
		}
	}

	[StoryCommand("debug_battle", "dbattle")]
	private async ValueTask ExecuteDebugBattleAsync(
		string battleId,
		CancellationToken cancellationToken)
	{
		var selectedCharacterIds = await UIRoot.Instance.ShowCombatantSelectPanelAsync(
			battleId,
			cancellationToken);
		await UIRoot.Instance.ShowBattleScreenAsync(
			new OrdinaryBattleRequest(battleId, selectedCharacterIds.ToArray()),
			cancellationToken);
	}
}
