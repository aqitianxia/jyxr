using Game.Application;
using Game.Godot.UI;

namespace Game.Godot.Story;

public sealed partial class GodotStoryRuntimeHost
{
	[StoryCommand("set_portrait", "head")]
	private ValueTask ExecuteHeadAsync(string characterId, string portraitId)
	{
		Game.CharacterService.SetCharacterPortrait(characterId, portraitId);
		return ValueTask.CompletedTask;
	}

	[StoryCommand("set_model", "animation")]
	private ValueTask ExecuteAnimationAsync(string characterId, string modelId)
	{
		Game.CharacterService.SetCharacterModel(characterId, modelId);
		return ValueTask.CompletedTask;
	}

	[StoryCommand("main_menu", "mainmenu")]
	private ValueTask ExecuteMainMenuAsync()
	{
		GameFlow.ReturnToMainMenu();
		return ValueTask.CompletedTask;
	}

	[StoryCommand("restart")]
	private async ValueTask ExecuteRestartAsync(CancellationToken cancellationToken) =>
		await GameFlow.StartNewGameAsync(cancellationToken);

	[StoryCommand("next_round", "nextzhoumu")]
	private ValueTask ExecuteNextZhoumuAsync(CancellationToken cancellationToken) =>
		new(GameFlow.StartNextRoundAsync(cancellationToken));

	[StoryCommand("game_over", "gameover")]
	private ValueTask ExecuteGameOverAsync()
	{
		GameFlow.GameOver();
		return ValueTask.CompletedTask;
	}

	[StoryCommand("game_complete", "gamefin")]
	private ValueTask ExecuteGameFinAsync()
	{
		UIRoot.Instance.ShowGameFinScreen();
		return ValueTask.CompletedTask;
	}
}
