namespace AntiAfk.Core.Constants;

public static class AppBranding
{
    public const string DisplayName = "AntiAFK";
    public const string TechnicalId = "antiafk";
    public const string MutexName = "Global\\antiafk.SingleInstance";

    // Fallback for when the Global\ name belongs to another user's session and cannot be opened.
    public const string LocalMutexName = "Local\\antiafk.SingleInstance";
    public const string AppUserModelId = "BxdiS.AntiAFK";
}
