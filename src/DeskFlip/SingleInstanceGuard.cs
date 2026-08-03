namespace DeskFlip;

/// <summary>
/// Enforces a single running instance via a named mutex. A second instance
/// signals the first to surface its settings window, then exits. The signal is a named
/// auto-reset event the first instance waits on from a thread-pool registration.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\DeskFlip.SingleInstance";
    private const string ShowEventName = @"Local\DeskFlip.ShowSettings";

    /// <summary>Raised (on a thread-pool thread) when another instance asks us to show settings.</summary>
    public event Action? ShowRequested;

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _waitRegistration;

    /// <summary>
    /// Returns true when this is the first instance (caller proceeds normally).
    /// Returns false when another instance is already running — it has been notified.
    /// </summary>
    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew)
        {
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            _waitRegistration = ThreadPool.RegisterWaitForSingleObject(
                _showEvent, (_, _) => ShowRequested?.Invoke(), null, -1, executeOnlyOnce: false);
            return true;
        }

        // Second instance: notify the running one and report failure.
        try
        {
            using var showEvent = EventWaitHandle.OpenExisting(ShowEventName);
            showEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // The first instance is mid-shutdown; nothing useful left to notify.
        }
        _mutex.Dispose();
        _mutex = null;
        return false;
    }

    public void Dispose()
    {
        _waitRegistration?.Unregister(null);
        _waitRegistration = null;
        _showEvent?.Dispose();
        _showEvent = null;
        if (_mutex != null)
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
            _mutex = null;
        }
    }
}
