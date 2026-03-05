using System.Runtime.InteropServices;
using System.Windows;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure;

public static partial class WindowThemeHelper
{
    // Windows 10 1809
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    // Windows 10 1903+ / 11
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [LibraryImport("dwmapi.dll", SetLastError = true)]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void ApplyTitleBarTheme(Window window, bool isDark)
    {
        if (window == null) return;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        int useDark = isDark ? 1 : 0;
        // Try newer attribute first
        _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        // Fallback for older builds
        _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDark, sizeof(int));
    }
}
