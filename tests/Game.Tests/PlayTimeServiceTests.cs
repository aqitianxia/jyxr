using Game.Application;
using Game.Core.Model;

namespace Game.Tests;

public sealed class PlayTimeServiceTests
{
    [Fact]
    public void Checkpoint_AdvancesSaveAndProfileUsingMonotonicTime()
    {
        var clock = new ManualTimeProvider();
        var session = CreateSession(clock);

        session.PlayTimeService.Start();
        clock.Advance(TimeSpan.FromSeconds(4.75));

        Assert.Equal(4, session.PlayTimeService.Checkpoint());
        Assert.Equal(4, session.State.PlayTimeSeconds);
        Assert.Equal(4, session.Profile.TotalPlayTimeSeconds);

        clock.Advance(TimeSpan.FromSeconds(0.25));
        Assert.Equal(1, session.PlayTimeService.Checkpoint());
        Assert.Equal(5, session.State.PlayTimeSeconds);
        Assert.Equal(5, session.Profile.TotalPlayTimeSeconds);
    }

    [Fact]
    public void Pause_ExcludesPausedTimeAndResumeContinues()
    {
        var clock = new ManualTimeProvider();
        var session = CreateSession(clock);

        session.PlayTimeService.Start();
        clock.Advance(TimeSpan.FromSeconds(2));
        session.PlayTimeService.Pause();
        clock.Advance(TimeSpan.FromMinutes(10));
        session.PlayTimeService.Resume();
        clock.Advance(TimeSpan.FromSeconds(3));
        session.PlayTimeService.Stop();

        Assert.Equal(5, session.State.PlayTimeSeconds);
        Assert.Equal(5, session.Profile.TotalPlayTimeSeconds);
        Assert.False(session.PlayTimeService.IsStarted);
        Assert.False(session.PlayTimeService.IsRunning);
    }

    [Fact]
    public void ResetInterval_DiscardsFractionalRemainder()
    {
        var clock = new ManualTimeProvider();
        var session = CreateSession(clock);

        session.PlayTimeService.Start();
        clock.Advance(TimeSpan.FromSeconds(0.75));
        session.PlayTimeService.Checkpoint();
        session.PlayTimeService.ResetInterval();
        clock.Advance(TimeSpan.FromSeconds(0.5));
        session.PlayTimeService.Checkpoint();

        Assert.Equal(0, session.State.PlayTimeSeconds);
        Assert.Equal(0, session.Profile.TotalPlayTimeSeconds);
    }

    [Fact]
    public void CreateSave_CheckpointsElapsedTimeBeforeSnapshot()
    {
        var clock = new ManualTimeProvider();
        var session = CreateSession(clock);
        session.PlayTimeService.Start();
        clock.Advance(TimeSpan.FromSeconds(7));

        var save = session.SaveGameService.CreateSave();

        Assert.Equal(7, save.PlayTimeSeconds);
        Assert.Equal(7, session.Profile.TotalPlayTimeSeconds);
    }

    [Fact]
    public void Checkpoint_DoesNotPartiallyMutateWhenEitherTotalOverflows()
    {
        var clock = new ManualTimeProvider();
        var profile = new GameProfile();
        profile.SetTotalPlayTimeSeconds(long.MaxValue);
        var session = new GameSession(
            new GameState(),
            TestContentFactory.CreateRepository(),
            initialProfile: profile,
            timeProvider: clock);
        session.PlayTimeService.Start();
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Throws<OverflowException>(() => session.PlayTimeService.Checkpoint());
        Assert.Equal(0, session.State.PlayTimeSeconds);
        Assert.Equal(long.MaxValue, session.Profile.TotalPlayTimeSeconds);
    }

    [Fact]
    public void Models_RejectNegativePlayTime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameState().SetPlayTimeSeconds(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameProfile().SetTotalPlayTimeSeconds(-1));
    }

    private static GameSession CreateSession(TimeProvider timeProvider) =>
        new(
            new GameState(),
            TestContentFactory.CreateRepository(),
            timeProvider: timeProvider);

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan elapsed) => _timestamp = checked(_timestamp + elapsed.Ticks);
    }
}
