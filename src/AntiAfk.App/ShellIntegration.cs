using System.Runtime.InteropServices;
using AntiAfk.Core.Constants;

namespace AntiAfk.App;

internal static class ShellIntegration
{
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string appId);

    public static void Register()
    {
        _ = SetCurrentProcessExplicitAppUserModelID(AppBranding.AppUserModelId);
    }
}
