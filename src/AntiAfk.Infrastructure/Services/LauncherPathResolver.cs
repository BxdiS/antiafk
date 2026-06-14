namespace AntiAfk.Infrastructure.Services;

public static class LauncherPathResolver
{
    public static string DefaultLauncherPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MajesticLauncher",
            "Majestic Launcher.exe");

    private static readonly string[] FallbackLauncherPaths =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "majestic-launcher", "Majestic Launcher.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Majestic Launcher", "Majestic Launcher.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Majestic Launcher", "Majestic Launcher.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Majestic Launcher", "Majestic Launcher.exe")
    ];

    public static string? Resolve(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        if (File.Exists(DefaultLauncherPath))
        {
            return DefaultLauncherPath;
        }

        foreach (var path in FallbackLauncherPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
