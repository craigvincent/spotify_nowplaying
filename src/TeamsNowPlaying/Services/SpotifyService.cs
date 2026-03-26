using System.Net;
using SpotifyAPI.Web;
using TeamsNowPlaying.Models;

namespace TeamsNowPlaying.Services;

public sealed class SpotifyService
{
    private const string CallbackUrl = "http://127.0.0.1:5543/callback";
    private const int CallbackPort = 5543;

    private readonly SettingsService _settings;
    private SpotifyClient? _client;
    private string? _connectedUser;

    public SpotifyService(SettingsService settings)
    {
        _settings = settings;
    }

    public bool IsConnected => _client is not null;
    public string? ConnectedUser => _connectedUser;

    public async Task<bool> TryRestoreConnectionAsync()
    {
        var refreshToken = _settings.Settings.Spotify.RefreshToken;
        var clientId = _settings.Settings.Spotify.ClientId;

        if (string.IsNullOrWhiteSpace(refreshToken) || string.IsNullOrWhiteSpace(clientId))
            return false;

        try
        {
            var response = await new OAuthClient().RequestToken(
                new PKCETokenRefreshRequest(clientId, refreshToken));

            if (!string.IsNullOrEmpty(response.RefreshToken))
            {
                _settings.Settings.Spotify.RefreshToken = response.RefreshToken;
                _settings.Save();
            }

            var authenticator = new PKCEAuthenticator(clientId, response);
            authenticator.TokenRefreshed += (_, token) =>
            {
                if (!string.IsNullOrEmpty(token.RefreshToken))
                {
                    _settings.Settings.Spotify.RefreshToken = token.RefreshToken;
                    _settings.Save();
                }
            };

            var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);
            _client = new SpotifyClient(config);

            var me = await _client.UserProfile.Current();
            _connectedUser = me.DisplayName ?? me.Id;
            return true;
        }
        catch
        {
            _client = null;
            _connectedUser = null;
            return false;
        }
    }

    public async Task<bool> ConnectAsync()
    {
        var clientId = _settings.Settings.Spotify.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
            return false;

        var (verifier, challenge) = PKCEUtil.GenerateCodes();

        var loginRequest = new LoginRequest(new Uri(CallbackUrl), clientId, LoginRequest.ResponseType.Code)
        {
            CodeChallengeMethod = "S256",
            CodeChallenge = challenge,
            Scope =
            [
                Scopes.UserReadCurrentlyPlaying,
                Scopes.UserReadPlaybackState
            ]
        };

        var authCode = await ListenForCallbackAsync(loginRequest.ToUri(), CancellationToken.None);
        if (authCode is null)
            return false;

        try
        {
            var tokenResponse = await new OAuthClient().RequestToken(
                new PKCETokenRequest(clientId, authCode, new Uri(CallbackUrl), verifier));

            _settings.Settings.Spotify.RefreshToken = tokenResponse.RefreshToken;
            _settings.Save();

            var authenticator = new PKCEAuthenticator(clientId, tokenResponse);
            authenticator.TokenRefreshed += (_, token) =>
            {
                if (!string.IsNullOrEmpty(token.RefreshToken))
                {
                    _settings.Settings.Spotify.RefreshToken = token.RefreshToken;
                    _settings.Save();
                }
            };

            var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);
            _client = new SpotifyClient(config);

            var me = await _client.UserProfile.Current();
            _connectedUser = me.DisplayName ?? me.Id;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Disconnect()
    {
        _client = null;
        _connectedUser = null;
        _settings.Settings.Spotify.RefreshToken = string.Empty;
        _settings.Save();
    }

    public async Task<TrackInfo?> GetCurrentlyPlayingAsync()
    {
        if (_client is null)
            return null;

        try
        {
            var playing = await _client.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());

            if (playing?.IsPlaying != true || playing.Item is not FullTrack track)
                return null;

            var artist = string.Join(", ", track.Artists.Select(a => a.Name));
            return new TrackInfo(artist, track.Name);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> ListenForCallbackAsync(Uri authUri, CancellationToken ct)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{CallbackPort}/");

        try
        {
            listener.Start();

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = authUri.ToString(),
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);

            var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromMinutes(3), ct);
            var code = context.Request.QueryString["code"];

            var responseBytes = System.Text.Encoding.UTF8.GetBytes(
                "<html><body><h2>You can close this tab and return to the app.</h2></body></html>");
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = responseBytes.Length;
            await context.Response.OutputStream.WriteAsync(responseBytes, ct);
            context.Response.Close();

            return code;
        }
        catch
        {
            return null;
        }
        finally
        {
            listener.Stop();
        }
    }
}
