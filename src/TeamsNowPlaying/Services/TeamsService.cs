using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Kiota.Abstractions.Authentication;

namespace TeamsNowPlaying.Services;

public sealed class TeamsService : IDisposable
{
    private static readonly string[] Scopes = ["Presence.ReadWrite", "User.Read"];

    private readonly SettingsService _settings;
    private IPublicClientApplication? _msalApp;
    private GraphServiceClient? _graphClient;
    private string? _connectedUser;
    private string? _userId;

    public TeamsService(SettingsService settings)
    {
        _settings = settings;
    }

    public bool IsConnected => _graphClient is not null;
    public string? ConnectedUser => _connectedUser;

    private IPublicClientApplication BuildMsalApp()
    {
        var teamsSettings = _settings.Settings.Teams;
        return PublicClientApplicationBuilder
            .Create(teamsSettings.ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, teamsSettings.TenantId)
            .WithRedirectUri("http://localhost")
            .Build();
    }

    private static async Task RegisterCacheAsync(IPublicClientApplication app)
    {
        var cacheDir = System.IO.Path.GetDirectoryName(SettingsService.GetMsalCachePath())!;
        var storageProperties = new StorageCreationPropertiesBuilder(
                System.IO.Path.GetFileName(SettingsService.GetMsalCachePath()), cacheDir)
            .Build();
        var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
        cacheHelper.RegisterCache(app.UserTokenCache);
    }

    public async Task<bool> TryRestoreConnectionAsync()
    {
        var clientId = _settings.Settings.Teams.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
            return false;

        try
        {
            _msalApp = BuildMsalApp();
            await RegisterCacheAsync(_msalApp);

            var accounts = await _msalApp.GetAccountsAsync();
            var account = accounts.GetEnumerator();
            if (!account.MoveNext())
                return false;

            var result = await _msalApp
                .AcquireTokenSilent(Scopes, account.Current)
                .ExecuteAsync();

            SetupGraphClient(result.AccessToken);
            await LoadUserInfoAsync();
            return true;
        }
        catch
        {
            _graphClient = null;
            _connectedUser = null;
            return false;
        }
    }

    public async Task<bool> ConnectAsync()
    {
        var clientId = _settings.Settings.Teams.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
            return false;

        try
        {
            _msalApp = BuildMsalApp();
            await RegisterCacheAsync(_msalApp);

            var result = await _msalApp
                .AcquireTokenInteractive(Scopes)
                .WithUseEmbeddedWebView(false)
                .ExecuteAsync();

            SetupGraphClient(result.AccessToken);
            await LoadUserInfoAsync();
            _settings.Save();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_msalApp is not null)
        {
            foreach (var account in await _msalApp.GetAccountsAsync())
            {
                await _msalApp.RemoveAsync(account);
            }
        }

        _graphClient = null;
        _connectedUser = null;
        _userId = null;
        _settings.Save();
    }

    public async Task SetStatusMessageAsync(string message)
    {
        if (_graphClient is null || _userId is null)
            return;

        await EnsureTokenFreshAsync();

        var body = new Microsoft.Graph.Users.Item.Presence.SetStatusMessage.SetStatusMessagePostRequestBody
        {
            StatusMessage = new PresenceStatusMessage
            {
                Message = new ItemBody
                {
                    Content = message,
                    ContentType = BodyType.Text
                },
                ExpiryDateTime = new DateTimeTimeZone
                {
                    DateTime = DateTimeOffset.UtcNow.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss.fffffffK", System.Globalization.CultureInfo.InvariantCulture),
                    TimeZone = "UTC"
                }
            }
        };

        await _graphClient.Users[_userId].Presence.SetStatusMessage.PostAsync(body);
    }

    public async Task ClearStatusMessageAsync()
    {
        if (_graphClient is null || _userId is null)
            return;

        await EnsureTokenFreshAsync();

        var body = new Microsoft.Graph.Users.Item.Presence.SetStatusMessage.SetStatusMessagePostRequestBody
        {
            StatusMessage = new PresenceStatusMessage
            {
                Message = new ItemBody
                {
                    Content = "",
                    ContentType = BodyType.Text
                }
            }
        };

        await _graphClient.Users[_userId].Presence.SetStatusMessage.PostAsync(body);
    }

    private void SetupGraphClient(string accessToken)
    {
        var authProvider = new BaseBearerTokenAuthenticationProvider(
            new TokenProvider(accessToken));
        _graphClient = new GraphServiceClient(authProvider);
    }

    private async Task LoadUserInfoAsync()
    {
        if (_graphClient is null)
            return;
        var me = await _graphClient.Me.GetAsync();
        _connectedUser = me?.DisplayName ?? me?.UserPrincipalName;
        _userId = me?.Id;
    }

    private async Task EnsureTokenFreshAsync()
    {
        if (_msalApp is null)
            return;

        try
        {
            var accounts = await _msalApp.GetAccountsAsync();
            var account = accounts.GetEnumerator();
            if (!account.MoveNext())
                return;

            var result = await _msalApp
                .AcquireTokenSilent(Scopes, account.Current)
                .ExecuteAsync();

            SetupGraphClient(result.AccessToken);
        }
        catch
        {
            // Token refresh failed; will fail on next Graph call
        }
    }

    public void Dispose()
    {
        (_graphClient as IDisposable)?.Dispose();
    }

    private sealed class TokenProvider(string token) : IAccessTokenProvider
    {
        public Task<string> GetAuthorizationTokenAsync(
            Uri uri,
            System.Collections.Generic.Dictionary<string, object>? additionalAuthenticationContext = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(token);
        }

        public AllowedHostsValidator AllowedHostsValidator { get; } = new();
    }
}
