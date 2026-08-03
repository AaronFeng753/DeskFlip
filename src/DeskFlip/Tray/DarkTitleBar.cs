using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DeskFlip.Tray;

/// <summary>
/// Applies the dark immersive title bar (DWMWA_USE_IMMERSIVE_DARK_MODE) to a window so
/// the non-client frame matches the in-app dark theme — no white flashbang.
/// Attribute id 20 works on Windows 10 20H1+ and Windows 11; 19 is the pre-20H1 alias.
/// Both no-op harmlessly on older OS builds.
/// </summary>
public static class DarkTitleBar
{
    private const int UseImmersiveDarkMode = 20;
    private const int UseImmersiveDarkModeLegacy = 19;

    /// <summary>Applies the dark bar once the window's HWND exists. Must be called before
    /// the window is first shown (both call sites do).</summary>
    public static void Apply(Window window) =>
        window.SourceInitialized += (_, _) => SetDark(window);

    private static void SetDark(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return;
        var enabled = 1;
        if (DwmSetWindowAttribute(handle, UseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
            DwmSetWindowAttribute(handle, UseImmersiveDarkModeLegacy, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
