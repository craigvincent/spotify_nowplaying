namespace TeamsNowPlaying.Models;

public sealed class SpotifySettings
{
    public string ClientId { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class TeamsSettings
{
    public string TenantId { get; set; } = "common";
    public string ClientId { get; set; } = string.Empty;
}

public sealed class AppSettings
{
    public SpotifySettings Spotify { get; set; } = new();
    public TeamsSettings Teams { get; set; } = new();
    public string Format { get; set; } = "\U0001f3b5 {artist} - {title}";
    public int PollIntervalSeconds { get; set; } = 10;
    public bool Enabled { get; set; } = true;
}
