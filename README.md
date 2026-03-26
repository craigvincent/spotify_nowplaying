# Teams Now Playing

A Windows system-tray application that publishes your currently playing Spotify track to your Microsoft Teams status message.

## Features

- 🎵 Polls Spotify for the currently playing track
- 💬 Updates your Microsoft Teams status message automatically
- ⏹️ Clears your status when nothing is playing
- ⚙️ Configurable status format with `{artist}` and `{title}` placeholders
- 🔄 Adjustable polling interval (5–60 seconds)
- 🖥️ System tray icon — runs quietly in the background

## Prerequisites

### 1. Spotify Developer App

1. Go to [Spotify Developer Dashboard](https://developer.spotify.com/dashboard)
2. Create a new app
3. Set the **Redirect URI** to: `http://localhost:5543/callback`
4. Copy the **Client ID**

### 2. Azure AD (Entra ID) App Registration

1. Go to [Azure Portal → App Registrations](https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade)
2. Click **New registration**
3. Set a name (e.g. "Teams Now Playing")
4. Under **Supported account types**, choose your organisation or "Accounts in any organizational directory"
5. Under **Redirect URI**, add **Public client/native** with value `http://localhost`
6. After creation, go to **API Permissions** → **Add a permission** → **Microsoft Graph** → **Delegated permissions**:
   - `Presence.ReadWrite`
   - `User.Read`
7. Go to **Authentication** → enable **Allow public client flows**
8. Copy the **Application (client) ID**

## Getting Started

```bash
# Clone and build
cd teams_nowplaying
dotnet build

# Run
dotnet run --project src/TeamsNowPlaying
```

On first launch the app will appear in your system tray. Right-click the tray icon and choose **Open Settings** to configure:

1. Paste your **Spotify Client ID** and click **Connect** (opens browser for login)
2. Paste your **Azure AD Client ID** and click **Connect** (opens browser for consent)
3. Customise the status format (default: `🎵 {artist} - {title}`)
4. Adjust the polling interval
5. Close the settings window — it hides to the tray

## Tech Stack

- [.NET 10](https://dotnet.microsoft.com/)
- [Avalonia UI 11](https://avaloniaui.net/) — cross-platform desktop UI
- [SpotifyAPI-NET](https://github.com/JohnnyCrazy/SpotifyAPI-NET) — Spotify Web API client
- [MSAL.NET](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet) — Microsoft identity authentication
- [Microsoft Graph SDK](https://github.com/microsoftgraph/msgraph-sdk-dotnet) — Teams presence API
