using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TeamsNowPlaying.Services;

namespace TeamsNowPlaying.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService _settings;
    private readonly SpotifyService _spotify;
    private readonly TeamsService _teams;
    private readonly NowPlayingService _nowPlaying;

    [ObservableProperty] private string _spotifyClientId = string.Empty;
    [ObservableProperty] private string _spotifyStatus = "Not connected";
    [ObservableProperty] private bool _isSpotifyConnected;

    [ObservableProperty] private string _teamsClientId = string.Empty;
    [ObservableProperty] private string _teamsTenantId = "common";
    [ObservableProperty] private string _teamsStatus = "Not connected";
    [ObservableProperty] private bool _isTeamsConnected;

    [ObservableProperty] private string _statusFormat = "\U0001f3b5 {artist} - {title}";
    [ObservableProperty] private int _pollIntervalSeconds = 10;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private string _lastUpdateStatus = string.Empty;
    [ObservableProperty] private string _currentTrackDisplay = "Nothing playing";

    // Bound in AXAML — must be instance property for data binding
    public string VersionDisplay { get; } = $"Version: {VersionInfo.Version}";

    // Design-time constructor
    public MainWindowViewModel()
    {
        _settings = null!;
        _spotify = null!;
        _teams = null!;
        _nowPlaying = null!;
    }

    public MainWindowViewModel(
        SettingsService settings,
        SpotifyService spotify,
        TeamsService teams,
        NowPlayingService nowPlaying)
    {
        _settings = settings;
        _spotify = spotify;
        _teams = teams;
        _nowPlaying = nowPlaying;

        LoadFromSettings();

        _nowPlaying.TrackChanged += track =>
        {
            CurrentTrackDisplay = track is not null
                ? track.Format("{artist} - {title}")
                : "Nothing playing";
        };

        _nowPlaying.StatusUpdated += status =>
        {
            LastUpdateStatus = status;
        };
    }

    private void LoadFromSettings()
    {
        var s = _settings.Settings;
        SpotifyClientId = s.Spotify.ClientId;
        TeamsClientId = s.Teams.ClientId;
        TeamsTenantId = s.Teams.TenantId;
        StatusFormat = s.Format;
        PollIntervalSeconds = s.PollIntervalSeconds;
        IsEnabled = s.Enabled;
    }

    private void SaveToSettings()
    {
        var s = _settings.Settings;
        s.Spotify.ClientId = SpotifyClientId;
        s.Teams.ClientId = TeamsClientId;
        s.Teams.TenantId = TeamsTenantId;
        s.Format = StatusFormat;
        s.PollIntervalSeconds = PollIntervalSeconds;
        s.Enabled = IsEnabled;
        _settings.Save();
    }

    [RelayCommand]
    private async Task ConnectSpotifyAsync()
    {
        SaveToSettings();
        SpotifyStatus = "Connecting...";
        var ok = await _spotify.ConnectAsync();
        IsSpotifyConnected = ok;
        SpotifyStatus = ok ? $"Connected as {_spotify.ConnectedUser}" : "Connection failed";
        if (ok)
            TryStartService();
    }

    [RelayCommand]
    private void DisconnectSpotify()
    {
        _spotify.Disconnect();
        IsSpotifyConnected = false;
        SpotifyStatus = "Not connected";
    }

    [RelayCommand]
    private async Task ConnectTeamsAsync()
    {
        SaveToSettings();
        TeamsStatus = "Connecting...";
        var ok = await _teams.ConnectAsync();
        IsTeamsConnected = ok;
        TeamsStatus = ok ? $"Connected as {_teams.ConnectedUser}" : "Connection failed";
        if (ok)
            TryStartService();
    }

    [RelayCommand]
    private async Task DisconnectTeamsAsync()
    {
        await _teams.DisconnectAsync();
        IsTeamsConnected = false;
        TeamsStatus = "Not connected";
    }

    partial void OnStatusFormatChanged(string value)
    {
        SaveToSettings();
    }

    partial void OnPollIntervalSecondsChanged(int value)
    {
        SaveToSettings();
        if (_nowPlaying.IsRunning)
            _nowPlaying.Restart();
    }

    partial void OnIsEnabledChanged(bool value)
    {
        SaveToSettings();
        if (value)
            TryStartService();
        else
            _nowPlaying.Stop();
    }

    public async Task TryRestoreConnectionsAsync()
    {
        if (await _spotify.TryRestoreConnectionAsync())
        {
            IsSpotifyConnected = true;
            SpotifyStatus = $"Connected as {_spotify.ConnectedUser}";
        }

        if (await _teams.TryRestoreConnectionAsync())
        {
            IsTeamsConnected = true;
            TeamsStatus = $"Connected as {_teams.ConnectedUser}";
        }

        TryStartService();
    }

    private void TryStartService()
    {
        if (IsEnabled && IsSpotifyConnected && IsTeamsConnected)
            _nowPlaying.Start();
    }
}
