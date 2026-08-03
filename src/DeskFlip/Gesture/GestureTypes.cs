namespace DeskFlip.Gesture;

/// <summary>A point in physical pixels, virtual-screen coordinates (may be negative).</summary>
public readonly record struct GesturePoint(int X, int Y);

/// <summary>Mouse buttons currently held. Mirrors the Win32 button set; flags so chords are representable.</summary>
[Flags]
public enum GestureButtons
{
    None = 0,
    Left = 1,
    Right = 2,
    Middle = 4,
    XButton1 = 8,
    XButton2 = 16,
}

public enum EdgeSide
{
    Left,
    Right,
}

/// <summary>
/// One exposed vertical monitor-edge segment in physical pixels, virtual-screen coordinates.
/// <see cref="X"/> is the edge column: the monitor's left edge for <see cref="EdgeSide.Left"/>,
/// the monitor's right edge (exclusive bound) for <see cref="EdgeSide.Right"/>.
/// The segment spans y ∈ [<see cref="Top"/>, <see cref="Bottom"/>).
/// <see cref="Scale"/> is the monitor's DPI scale (physical/logical, e.g. 2.0 at 200%):
/// user-facing pixel parameters are logical and are multiplied by this factor.
/// </summary>
public readonly record struct EdgeSegment(EdgeSide Side, int X, int Top, int Bottom, double Scale)
{
    public EdgeSegment(EdgeSide side, int x, int top, int bottom) : this(side, x, top, bottom, 1.0)
    {
    }

    public bool ContainsY(int y) => y >= Top && y < Bottom;
}

public enum SwitchDirection
{
    Left,
    Right,
}
