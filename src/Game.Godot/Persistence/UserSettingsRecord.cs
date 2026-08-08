namespace Game.Godot.Persistence;

using global::Game.Godot.Settings;

public sealed record UserSettingsRecord(
	int Version,
	bool ShowBattleHp,
	bool AutoSave,
	bool AutoBattle,
	bool BattleSpeedUp,
	int BattleSpeedMultiplier,
	bool MusicEnabled,
	bool SfxEnabled,
	bool DialogueTypewriterEnabled,
	bool ShowBattleBoard,
	bool LargeMapMovementAnimationEnabled,
	ScreenAspectMode ScreenAspectMode,
	float LargeMapZoom)
{
	public const int CurrentVersion = 7;

	public static UserSettingsRecord Default { get; } = new(
		CurrentVersion,
		ShowBattleHp: true,
		AutoSave: true,
		AutoBattle: false,
		BattleSpeedUp: false,
		BattleSpeedMultiplier: 2,
		MusicEnabled: true,
		SfxEnabled: true,
		DialogueTypewriterEnabled: true,
		ShowBattleBoard: true,
		LargeMapMovementAnimationEnabled: true,
		ScreenAspectMode: ScreenAspectMode.Unlimited,
		LargeMapZoom: 0f);
}
