using SpotifyNowPlaying.Models;

namespace SpotifyNowPlaying.Services;

public sealed class NowPlayingService : INowPlayingService, IDisposable
{
    private readonly ISpotifyService _spotify;
    private readonly ITeamsService _teams;
    private readonly ISettingsService _settings;
    private readonly TimeProvider _timeProvider;

    private CancellationTokenSource? _cts;
    private Task? _pollingTask;
    private TrackInfo? _lastTrack;
    private bool _lastWasCleared;

    public NowPlayingService(ISpotifyService spotify, ITeamsService teams, ISettingsService settings, TimeProvider? timeProvider = null)
    {
        _spotify = spotify;
        _teams = teams;
        _settings = settings;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public TrackInfo? CurrentTrack => _lastTrack;
    public bool IsRunning => _pollingTask is not null && !_pollingTask.IsCompleted;

    public event Action<TrackInfo?>? TrackChanged;
    public event Action<string>? StatusUpdated;

    public void Start()
    {
        Stop();

        if (!_settings.Settings.Enabled)
            return;

        _cts = new CancellationTokenSource();
        _pollingTask = PollLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _pollingTask = null;
    }

    public void Restart()
    {
        Stop();
        Start();
    }

    public void Dispose()
    {
        Stop();
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var interval = TimeSpan.FromSeconds(
                    Math.Max(5, _settings.Settings.PollIntervalSeconds));

                await Task.Delay(interval, _timeProvider, ct);

                if (!_spotify.IsConnected || !_teams.IsConnected)
                    continue;

                var track = await _spotify.GetCurrentlyPlayingAsync();

                if (track is not null)
                {
                    if (_lastTrack is null || _lastTrack != track)
                    {
                        _lastTrack = track;
                        _lastWasCleared = false;

                        var message = track.Format(_settings.Settings.Format);
                        await _teams.SetStatusMessageAsync(message);

                        TrackChanged?.Invoke(track);
                        StatusUpdated?.Invoke($"Updated: {message}");
                    }
                }
                else if (!_lastWasCleared)
                {
                    _lastTrack = null;
                    _lastWasCleared = true;

                    await _teams.ClearStatusMessageAsync();

                    TrackChanged?.Invoke(null);
                    StatusUpdated?.Invoke("Cleared status (nothing playing)");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                StatusUpdated?.Invoke($"Error: {ex.Message}");
            }
        }
    }
}
