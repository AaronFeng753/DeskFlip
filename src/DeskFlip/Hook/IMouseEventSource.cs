using DeskFlip.Gesture;

namespace DeskFlip.Hook;

/// <summary>
/// Source of global mouse-move samples. Implementations raise <see cref="MouseMoved"/>
/// on an unspecified background thread; subscribers must marshal as needed.
/// Kept behind an interface so tests never install a real hook.
/// </summary>
public interface IMouseEventSource
{
    event Action<GesturePoint, GestureButtons, DateTime>? MouseMoved;

    /// <summary>Human-readable lifecycle events (hook installed/reinstalled), for the diagnostic log.</summary>
    event Action<string>? Trace;
    void Start();
    void Stop();
}
