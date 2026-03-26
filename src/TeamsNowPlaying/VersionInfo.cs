using System.Reflection;

namespace TeamsNowPlaying;

public static class VersionInfo
{
    public static string Version { get; } = GetVersion();

    private static string GetVersion()
    {
        var attr = typeof(VersionInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        return attr?.InformationalVersion ?? "dev";
    }
}
