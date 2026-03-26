namespace TeamsNowPlaying.Models;

public sealed record TrackInfo(string Artist, string Title)
{
    public string Format(string template) =>
        template.Replace("{artist}", Artist).Replace("{title}", Title);
}
