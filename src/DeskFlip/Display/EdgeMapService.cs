using System.Runtime.InteropServices;
using DeskFlip.Gesture;
using Microsoft.Win32;

namespace DeskFlip.Display;

/// <summary>
/// A monitor rectangle in physical pixels, virtual-screen coordinates (may be negative).
/// <see cref="Scale"/> is the monitor's DPI scale (physical/logical, e.g. 2.0 at 200%).
/// </summary>
public readonly record struct MonitorRect(int Left, int Top, int Right, int Bottom, double Scale)
{
    public MonitorRect(int left, int top, int right, int bottom) : this(left, top, right, bottom, 1.0)
    {
    }

    public bool ContainsX(int x) => x >= Left && x < Right;
}

/// <summary>
/// Computes and caches the "exposed vertical edge segments" of all monitors:
/// the portions of each monitor's left/right edge whose immediate outside pixel is not
/// covered by any other monitor. Cursor-clamping by the OS makes these outer edges
/// infinitely wide targets; inner (shared) edges are excluded so the cursor crosses
/// monitors freely without triggering.
/// Recomputes on display-setting changes and session unlock.
/// </summary>
public sealed class EdgeMapService : IDisposable
{
    public event Action<IReadOnlyList<EdgeSegment>>? EdgeMapChanged;

    private readonly Func<IReadOnlyList<MonitorRect>> _monitorSource;

    public IReadOnlyList<EdgeSegment> Segments { get; private set; } = Array.Empty<EdgeSegment>();

    /// <summary>Production constructor: reads real monitor geometry and subscribes to system events.</summary>
    public EdgeMapService() : this(QueryMonitors)
    {
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        Refresh();
    }

    /// <summary>Test constructor: custom monitor source, no system-event subscription.</summary>
    public EdgeMapService(Func<IReadOnlyList<MonitorRect>> monitorSource)
    {
        _monitorSource = monitorSource;
    }

    public void Refresh()
    {
        var segments = ComputeExposedSegments(_monitorSource());
        Segments = segments;
        EdgeMapChanged?.Invoke(segments);
    }

    /// <summary>
    /// Pure core: given all monitor rectangles, compute exposed left/right edge segments.
    /// For each edge column, the y-intervals where the adjacent outside pixel lies inside
    /// another monitor are subtracted (interval subtraction).
    /// </summary>
    public static IReadOnlyList<EdgeSegment> ComputeExposedSegments(IReadOnlyList<MonitorRect> monitors)
    {
        var result = new List<EdgeSegment>();
        for (var i = 0; i < monitors.Count; i++)
        {
            var m = monitors[i];
            foreach (var side in new[] { EdgeSide.Left, EdgeSide.Right })
            {
                // The pixel column immediately outside this edge.
                var outsideX = side == EdgeSide.Left ? m.Left - 1 : m.Right;
                var edgeX = side == EdgeSide.Left ? m.Left : m.Right;

                var intervals = new List<(int Top, int Bottom)> { (m.Top, m.Bottom) };
                for (var j = 0; j < monitors.Count; j++)
                {
                    if (j == i)
                        continue;
                    var other = monitors[j];
                    if (!other.ContainsX(outsideX))
                        continue;
                    intervals = Subtract(intervals, other.Top, other.Bottom);
                }

                foreach (var (top, bottom) in intervals)
                    result.Add(new EdgeSegment(side, edgeX, top, bottom, m.Scale));
            }
        }
        return result;
    }

    private static List<(int Top, int Bottom)> Subtract(
        List<(int Top, int Bottom)> intervals, int cutTop, int cutBottom)
    {
        var remaining = new List<(int, int)>();
        foreach (var (top, bottom) in intervals)
        {
            if (cutBottom <= top || cutTop >= bottom)
            {
                remaining.Add((top, bottom)); // no overlap
                continue;
            }
            if (cutTop > top)
                remaining.Add((top, cutTop));
            if (cutBottom < bottom)
                remaining.Add((cutBottom, bottom));
        }
        return remaining;
    }

    private void OnDisplayChanged(object? sender, EventArgs e) => Refresh();

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionUnlock)
            Refresh();
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }

    private static IReadOnlyList<MonitorRect> QueryMonitors()
    {
        var monitors = new List<MonitorRect>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref NativeRect rect, IntPtr data) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(hMonitor, ref info))
                monitors.Add(new MonitorRect(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right, info.Monitor.Bottom,
                    QueryScale(hMonitor)));
            return true;
        }, IntPtr.Zero);
        return monitors;
    }

    private static double QueryScale(IntPtr hMonitor) =>
        GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0
            ? dpiX / 96.0
            : 1.0;

    private const int MDT_EFFECTIVE_DPI = 0;

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref NativeRect lprcMonitor, IntPtr dwData);

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
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);
}
