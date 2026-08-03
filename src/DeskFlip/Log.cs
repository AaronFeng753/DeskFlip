using System.IO;

namespace DeskFlip;

/// <summary>
/// Optional diagnostic log at %AppData%\DeskFlip\deskflip.log.
/// Disabled by default — while <see cref="Enabled"/> is false, writes are
/// no-ops and the file is never even created (lazy open). Self-trimming: beyond ~50 KB
/// the file is cut back to its last ~25 KB. Logging must never crash the app, so I/O
/// failures degrade to silence.
/// </summary>
public static class Log
{
    private const long MaxBytes = 50_000;
    private const int KeepBytesAfterTrim = 25_000;

    private static readonly object Gate = new();
    private static StreamWriter? _writer;
    private static bool _sessionMarked;

    /// <summary>Master switch, hot-swappable from the settings GUI. Off by default.</summary>
    public static bool Enabled { get; set; }

    public static string FilePath { get; private set; } = string.Empty;

    public static void Init(string configDirectory)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(configDirectory);
                FilePath = Path.Combine(configDirectory, "deskflip.log");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                FilePath = string.Empty;
            }
        }
    }

    public static void Write(string message)
    {
        lock (Gate)
        {
            if (!Enabled || FilePath.Length == 0)
                return;
            try
            {
                EnsureOpen();
                if (_writer == null)
                    return;
                _writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
                if (_writer.BaseStream.Position > MaxBytes)
                    Trim();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ObjectDisposedException)
            {
                _writer = null; // degrade to silence
            }
        }
    }

    private static void EnsureOpen()
    {
        if (_writer != null)
            return;
        var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream) { AutoFlush = true };
        if (!_sessionMarked)
        {
            _sessionMarked = true;
            _writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} --- session start ---");
        }
    }

    private static void Trim()
    {
        _writer?.Dispose();
        _writer = null;
        var text = File.ReadAllText(FilePath);
        if (text.Length > KeepBytesAfterTrim)
        {
            // Cut at a line boundary so the tail stays parseable.
            var cut = text.IndexOf('\n', text.Length - KeepBytesAfterTrim);
            text = cut >= 0 ? text[(cut + 1)..] : text[^KeepBytesAfterTrim..];
        }
        File.WriteAllText(FilePath, text);
        EnsureOpen();
        _writer?.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} --- trimmed to last ~{KeepBytesAfterTrim / 1000} KB ---");
    }

    public static void Close()
    {
        lock (Gate)
        {
            try
            {
                if (Enabled)
                    _writer?.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} --- session end ---");
                _writer?.Dispose();
            }
            catch (IOException)
            {
                // Shutdown must not fail over the log.
            }
            _writer = null;
        }
    }
}
