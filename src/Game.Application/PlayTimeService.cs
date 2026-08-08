namespace Game.Application;

public sealed class PlayTimeService
{
    private readonly GameSession _session;
    private readonly TimeProvider _timeProvider;
    private long _lastTimestamp;
    private TimeSpan _pendingElapsed;

    public PlayTimeService(GameSession session, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsStarted { get; private set; }

    public bool IsRunning { get; private set; }

    public void Start()
    {
        if (IsStarted)
        {
            Resume();
            return;
        }

        IsStarted = true;
        IsRunning = true;
        _lastTimestamp = _timeProvider.GetTimestamp();
    }

    public void Pause()
    {
        if (!IsRunning)
        {
            return;
        }

        Checkpoint();
        IsRunning = false;
    }

    public void Resume()
    {
        if (!IsStarted || IsRunning)
        {
            return;
        }

        IsRunning = true;
        _lastTimestamp = _timeProvider.GetTimestamp();
    }

    public long Checkpoint()
    {
        if (!IsRunning)
        {
            return 0;
        }

        var timestamp = _timeProvider.GetTimestamp();
        var elapsed = _timeProvider.GetElapsedTime(_lastTimestamp, timestamp);
        if (elapsed < TimeSpan.Zero)
        {
            throw new InvalidOperationException("The play-time clock moved backwards.");
        }

        _lastTimestamp = timestamp;
        _pendingElapsed += elapsed;

        var seconds = _pendingElapsed.Ticks / TimeSpan.TicksPerSecond;
        if (seconds == 0)
        {
            return 0;
        }

        var stateTotal = checked(_session.State.PlayTimeSeconds + seconds);
        var profileTotal = checked(_session.Profile.TotalPlayTimeSeconds + seconds);
        _session.State.SetPlayTimeSeconds(stateTotal);
        _session.Profile.SetTotalPlayTimeSeconds(profileTotal);
        _pendingElapsed -= TimeSpan.FromSeconds(seconds);
        return seconds;
    }

    public long Stop()
    {
        var addedSeconds = Checkpoint();
        IsRunning = false;
        IsStarted = false;
        return addedSeconds;
    }

    public void ResetInterval()
    {
        _pendingElapsed = TimeSpan.Zero;
        if (IsRunning)
        {
            _lastTimestamp = _timeProvider.GetTimestamp();
        }
    }
}
