using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SpotifyNowPlaying.Models;
using SpotifyNowPlaying.Services;
using Xunit;

namespace SpotifyNowPlaying.Tests.Services;

public class NowPlayingServiceTests : IDisposable
{
    private readonly ISpotifyService _spotify;
    private readonly ITeamsService _teams;
    private readonly ISettingsService _settings;
    private readonly FakeTimeProvider _timeProvider;
    private readonly NowPlayingService _service;

    public NowPlayingServiceTests()
    {
        _spotify = Substitute.For<ISpotifyService>();
        _teams = Substitute.For<ITeamsService>();
        _settings = Substitute.For<ISettingsService>();
        _settings.Settings.Returns(new AppSettings
        {
            PollIntervalSeconds = 5,
            Enabled = true,
            Format = "\U0001f3b5 {artist} - {title}"
        });

        _spotify.IsConnected.Returns(true);
        _teams.IsConnected.Returns(true);

        _timeProvider = new FakeTimeProvider();
        _service = new NowPlayingService(_spotify, _teams, _settings, _timeProvider);
    }

    [Fact]
    public void Start_WhenDisabled_DoesNotRun()
    {
        _settings.Settings.Enabled = false;

        _service.Start();

        Assert.False(_service.IsRunning);
    }

    [Fact]
    public void Start_WhenEnabled_IsRunning()
    {
        _service.Start();

        Assert.True(_service.IsRunning);
    }

    [Fact]
    public void Stop_AfterStart_IsNotRunning()
    {
        _service.Start();
        _service.Stop();

        Assert.False(_service.IsRunning);
    }

    [Fact]
    public void Restart_StartsFreshPolling()
    {
        _service.Start();
        Assert.True(_service.IsRunning);

        _service.Restart();
        Assert.True(_service.IsRunning);
    }

    [Fact]
    public async Task PollLoop_WithTrack_SetsTeamsStatus()
    {
        var track = new TrackInfo("Radiohead", "Creep");
        _spotify.GetCurrentlyPlayingAsync().Returns(track);

        TrackInfo? changedTrack = null;
        string? statusMsg = null;
        _service.TrackChanged += t => changedTrack = t;
        _service.StatusUpdated += s => statusMsg = s;

        _service.Start();

        // Advance fake time past the poll interval
        _timeProvider.Advance(TimeSpan.FromSeconds(6));
        await Task.Yield();

        _service.Stop();

        await _teams.Received().SetStatusMessageAsync("\U0001f3b5 Radiohead - Creep");
        Assert.Equal(track, changedTrack);
        Assert.Contains("Radiohead", statusMsg);
    }

    [Fact]
    public async Task PollLoop_NoTrack_ClearsStatus()
    {
        _spotify.GetCurrentlyPlayingAsync().Returns((TrackInfo?)null);

        string? statusMsg = null;
        _service.StatusUpdated += s => statusMsg = s;

        _service.Start();

        _timeProvider.Advance(TimeSpan.FromSeconds(6));
        await Task.Yield();

        _service.Stop();

        await _teams.Received().ClearStatusMessageAsync();
        Assert.Contains("Cleared", statusMsg);
    }

    [Fact]
    public async Task PollLoop_ServicesNotConnected_DoesNotCallApis()
    {
        _spotify.IsConnected.Returns(false);
        _teams.IsConnected.Returns(false);

        _service.Start();

        _timeProvider.Advance(TimeSpan.FromSeconds(6));
        await Task.Yield();

        _service.Stop();

        await _spotify.DidNotReceive().GetCurrentlyPlayingAsync();
    }

    [Fact]
    public async Task PollLoop_SameTrackTwice_SetsStatusOnlyOnce()
    {
        var track = new TrackInfo("Radiohead", "Creep");
        _spotify.GetCurrentlyPlayingAsync().Returns(track);

        _service.Start();

        // Advance past two poll cycles
        _timeProvider.Advance(TimeSpan.FromSeconds(6));
        await Task.Yield();
        _timeProvider.Advance(TimeSpan.FromSeconds(6));
        await Task.Yield();

        _service.Stop();

        // Should only set status once since track didn't change
        await _teams.Received(1).SetStatusMessageAsync(Arg.Any<string>());
    }

    [Fact]
    public void Dispose_StopsPolling()
    {
        _service.Start();
        Assert.True(_service.IsRunning);

        _service.Dispose();
        Assert.False(_service.IsRunning);
    }

    public void Dispose()
    {
        _service.Dispose();
    }
}
