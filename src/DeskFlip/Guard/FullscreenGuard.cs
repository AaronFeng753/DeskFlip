using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeskFlip.Guard;

/// <summary>
/// Polls (~300 ms) whether any fullscreen application is active. Fullscreen
/// when either:
/// 1. the foreground window's DWM extended frame bounds fully cover its monitor within a
///    few pixels of tolerance (primary heuristic), excluding shell processes, or
/// 2. SHQueryUserNotificationState reports QUNS_RUNNING_D3D_FULL_SCREEN (fallback for the
///    rare true-exclusive case; Fullscreen Optimizations usually reports only QUNS_BUSY).
/// Session-global by design: fullscreen on any monitor disables gestures everywhere.
/// </summary>
public sealed class FullscreenGuard : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(300);
    private const int CoverageTolerancePx = 8;

    // Shell processes excluded by name: more robust against Win11 version
    // churn than window-class lists.
    private static readonly HashSet<string> ShellProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer",
        "ShellExperienceHost",
        "SearchHost",
    };

    /// <summary>Raised (on a thread-pool thread) when the fullscreen state changes.</summary>
    public event Action<bool>? FullscreenChanged;

    private Timer? _timer;
    private bool _isFullscreen;

    public bool IsFullscreen
    {
        get => _isFullscreen;
        private set
        {
            if (_isFullscreen == value)
                return;
            _isFullscreen = value;
            FullscreenChanged?.Invoke(value);
        }
    }

    public void Start() => _timer ??= new Timer(_ => IsFullscreen = DetectFullscreen(), null, TimeSpan.Zero, PollInterval);

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => Stop();

    /// <summary>Current fullscreen verdict, computed on demand. Public for diagnostics and testing.</summary>
    public static bool DetectFullscreen()
    {
        if (QueryUserNotificationStateIsD3DFullscreen())
            return true;

        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
            return false; // transient during foreground switches: treat as not fullscreen

        if (DwmGetWindowAttribute(foreground, DWMWA_EXTENDED_FRAME_BOUNDS, out var frame, Marshal.SizeOf<NativeRect>()) != 0)
            return false;

        if (IsShellProcess(foreground))
            return false;

        var monitor = MonitorFromWindow(foreground, MONITOR_DEFAULTTONEAREST);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
            return false;

        var m = info.Monitor;
        return frame.Left <= m.Left + CoverageTolerancePx
            && frame.Top <= m.Top + CoverageTolerancePx
            && frame.Right >= m.Right - CoverageTolerancePx
            && frame.Bottom >= m.Bottom - CoverageTolerancePx;
    }

    private static bool IsShellProcess(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var pid);
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return ShellProcessNames.Contains(process.ProcessName);
        }
        catch (ArgumentException)
        {
            // Process exited between the snapshot and now: treat as a normal app.
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool QueryUserNotificationStateIsD3DFullscreen() =>
        SHQueryUserNotificationState(out var state) == 0 && state == QUNS_RUNNING_D3D_FULL_SCREEN;

    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const int MONITOR_DEFAULTTONEAREST = 2;
    private const int QUNS_RUNNING_D3D_FULL_SCREEN = 6;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr hwnd, int dwAttribute, out NativeRect pvAttribute, int cbAttribute);

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int pquns);
}
