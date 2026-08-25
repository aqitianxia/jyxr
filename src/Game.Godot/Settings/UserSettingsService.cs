using Game.Godot.Persistence;

namespace Game.Godot.Settings;

public sealed class UserSettingsService
{
	private readonly LocalUserSettingsStore _store;

	public UserSettingsService(LocalUserSettingsStore store, UserSettingsRecord initialSettings)
	{
		_store = store ?? throw new ArgumentNullException(nameof(store));
		Current = initialSettings ?? throw new ArgumentNullException(nameof(initialSettings));
	}

	public UserSettingsRecord Current { get; private set; }

	public void ApplyCurrent() => UserSettingsApplier.Apply(Current);

	public void Update(Func<UserSettingsRecord, UserSettingsRecord> update)
	{
		ArgumentNullException.ThrowIfNull(update);

		var previous = Current;
		var updated = update(previous) ?? throw new InvalidOperationException("User settings update returned null.");
		if (updated.Version != UserSettingsRecord.CurrentVersion)
		{
			throw new InvalidOperationException(
				$"User settings version must be {UserSettingsRecord.CurrentVersion}, but was {updated.Version}.");
		}

		if (updated == previous)
		{
			return;
		}

		try
		{
			UserSettingsApplier.Apply(updated);
			_store.Save(updated);
			Current = updated;
		}
		catch
		{
			UserSettingsApplier.Apply(previous);
			throw;
		}
	}
}
