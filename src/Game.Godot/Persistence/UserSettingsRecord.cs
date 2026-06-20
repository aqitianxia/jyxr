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
	ScreenAspectMode ScreenAspectMode)
{
	public const int CurrentVersion = 3;

	public static UserSettingsRecord Default { get; } = new(
		CurrentVersion,
		ShowBattleHp: true,
		AutoSave: true,
		AutoBattle: false,
		BattleSpeedUp: false,
		BattleSpeedMultiplier: 2,
		MusicEnabled: true,
		SfxEnabled: true,
		ScreenAspectMode: ScreenAspectMode.Unlimited);
}
