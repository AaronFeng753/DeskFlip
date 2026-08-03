using System.Diagnostics;

namespace DeskFlip.Guard;

/// <summary>
/// Polls a process snapshot every second: gestures are disabled while any
/// user-blocklisted process exists (foreground not required). Names are compared without
/// ".exe", case-insensitive — the same convention as <see cref="Process.ProcessName"/>.
/// </summary>
public sealed class BlocklistMonitor : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <summary>Raised (on a thread-pool thread) when the blocked state changes.</summary>
    public event Action<bool>? BlockedChanged;

    private readonly object _gate = new();
    private HashSet<string> _blockedNames = new(StringComparer.OrdinalIgnoreCase);
    private Timer? _timer;
    private bool _isBlocked;

    public bool IsBlocked
    {
        get { lock (_gate) return _isBlocked; }
        private set
        {
            lock (_gate)
            {
                if (_isBlocked == value)
                    return;
                _isBlocked = value;
            }
            BlockedChanged?.Invoke(value);
        }
    }

    public void SetBlockedNames(IEnumerable<string> names)
    {
        lock (_gate)
            _blockedNames = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
    }

    public void Start() => _timer ??= new Timer(_ => IsBlocked = DetectBlocked(), null, TimeSpan.Zero, PollInterval);

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => Stop();

    private bool DetectBlocked()
    {
        HashSet<string> names;
        lock (_gate)
            names = _blockedNames;
        if (names.Count == 0)
            return false;

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                string name;
                try
                {
                    name = process.ProcessName;
                }
                catch (InvalidOperationException)
                {
                    continue; // exited mid-snapshot
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    continue; // protected process: cannot read its name
                }
                if (names.Contains(name))
                    return true;
            }
        }
        return false;
    }
}
