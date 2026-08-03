using System.Windows;
using System.Windows.Media;
using DeskFlip.Config;
using DeskFlip.Gesture;

namespace DeskFlip.Tray;

/// <summary>
/// Semi-transparent, click-through overlays visualizing the edge zones while the
/// settings window is open: hold zone (green) behind the trigger zone
/// (blue). Rebuilt on every call to <see cref="Show"/> so slider/typed changes appear
/// immediately. Zone widths are logical pixels — the overlay draws them 1:1 because WPF
/// window coordinates are logical too (segment physical px ÷ monitor scale).
/// </summary>
public sealed class ZoneVisualizer
{
    private static readonly Color HoldColor = Colors.LimeGreen;
    private static readonly Color TriggerColor = Colors.DodgerBlue;
    private const double HoldOpacity = 0.18;
    private const double TriggerOpacity = 0.40;

    private readonly List<Window> _windows = new();

    public bool IsShowing => _windows.Count > 0;

    public void Show(IReadOnlyList<EdgeSegment> segments, AppSettings settings)
    {
        Hide();
        foreach (var seg in segments)
        {
            var left = seg.X / seg.Scale;
            var top = seg.Top / seg.Scale;
            var height = (seg.Bottom - seg.Top) / seg.Scale;
            // Hold zone first (underneath), then the narrower trigger zone on top.
            AddZone(seg.Side, left, top, Math.Max(settings.HoldZoneWidth, settings.TriggerWidth), height, HoldColor, HoldOpacity);
            AddZone(seg.Side, left, top, settings.TriggerWidth, height, TriggerColor, TriggerOpacity);
        }
    }

    private void AddZone(EdgeSide side, double edgeX, double top, double width, double height,
        Color color, double opacity)
    {
        if (height <= 0 || width <= 0)
            return;
        // WPF enforces a system minimum window width (~12 logical px): a narrow zone
        // window gets silently widened, and on the right edge the surplus spills
        // off-screen so the zone looks narrower than on the left. Host an exact-width
        // rectangle inside a wider transparent window instead; the anchor alignment
        // keeps the visible zone exact no matter how wide the host gets clamped.
        var windowWidth = Math.Max(width, 32);
        var zone = new System.Windows.Shapes.Rectangle
        {
            Width = width,
            Fill = new SolidColorBrush(color),
            HorizontalAlignment = side == EdgeSide.Left ? HorizontalAlignment.Left : HorizontalAlignment.Right,
        };
        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Opacity = opacity,
            ShowInTaskbar = false,
            Topmost = true,
            ShowActivated = false,
            IsHitTestVisible = false, // click-through: pure decoration
            Focusable = false,
            Content = zone,
            Left = side == EdgeSide.Left ? edgeX : edgeX - windowWidth,
            Top = top,
            Width = windowWidth,
            Height = height,
        };
        window.Show();
        _windows.Add(window);
    }

    public void Hide()
    {
        foreach (var window in _windows)
            window.Close();
        _windows.Clear();
    }
}
