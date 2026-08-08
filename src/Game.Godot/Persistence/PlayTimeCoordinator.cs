using Game.Application;
using Godot;

namespace Game.Godot.Persistence;

public partial class PlayTimeCoordinator : Node
{
	private const double PersistenceIntervalSeconds = 60d;
	private readonly LocalProfileStore _profileStore = new();
	private GameSession? _session;
	private bool _isGameplayActive;
	private bool _hasApplicationFocus = true;
	private bool _wasTreePaused;
	private double _persistenceElapsed;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		_wasTreePaused = GetTree().Paused;
	}

	public override void _Process(double delta)
	{
		var isTreePaused = GetTree().Paused;
		if (isTreePaused != _wasTreePaused)
		{
			_wasTreePaused = isTreePaused;
			SynchronizeRunningState();
			if (isTreePaused)
			{
				PersistProfile();
			}
		}

		if (!ShouldRun())
		{
			return;
		}

		_persistenceElapsed += delta;
		if (_persistenceElapsed < PersistenceIntervalSeconds)
		{
			return;
		}

		_persistenceElapsed = 0d;
		CheckpointAndPersist();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationApplicationFocusOut)
		{
			_hasApplicationFocus = false;
			SynchronizeRunningState();
			PersistProfile();
		}
		else if (what == NotificationApplicationFocusIn)
		{
			_hasApplicationFocus = true;
			SynchronizeRunningState();
		}
	}

	public override void _ExitTree()
	{
		if (_session is null)
		{
			return;
		}

		_session.PlayTimeService.Stop();
		PersistProfile();
		_session = null;
	}

	public void Bind(GameSession session)
	{
		ArgumentNullException.ThrowIfNull(session);
		_session = session;
		_isGameplayActive = false;
		_persistenceElapsed = 0d;
	}

	public void StartGameplay()
	{
		EnsureBound();
		_isGameplayActive = true;
		_persistenceElapsed = 0d;
		SynchronizeRunningState();
	}

	public void StopGameplay()
	{
		if (_session is null)
		{
			return;
		}

		_isGameplayActive = false;
		_persistenceElapsed = 0d;
		_session.PlayTimeService.Stop();
		PersistProfile();
	}

	private bool ShouldRun() =>
		_session is not null &&
		_isGameplayActive &&
		_hasApplicationFocus &&
		!GetTree().Paused;

	private void SynchronizeRunningState()
	{
		if (_session is null)
		{
			return;
		}

		if (!ShouldRun())
		{
			_session.PlayTimeService.Pause();
			return;
		}

		if (_session.PlayTimeService.IsStarted)
		{
			_session.PlayTimeService.Resume();
		}
		else
		{
			_session.PlayTimeService.Start();
		}
	}

	private void CheckpointAndPersist()
	{
		_session?.PlayTimeService.Checkpoint();
		PersistProfile();
	}

	private void PersistProfile()
	{
		if (_session is null || !Game.IsInitialized)
		{
			return;
		}

		try
		{
			_profileStore.SaveCurrentProfile();
		}
		catch (Exception exception)
		{
			Game.Logger.Error("Persisting play time failed.", exception);
		}
	}

	private void EnsureBound()
	{
		if (_session is null)
		{
			throw new InvalidOperationException("Play-time coordinator is not bound to a game session.");
		}
	}
}
