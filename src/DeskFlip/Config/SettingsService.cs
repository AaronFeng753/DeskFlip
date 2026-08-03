using System.IO;
using System.Text.Json;

namespace DeskFlip.Config;

/// <summary>
/// JSON settings at %AppData%\DeskFlip\settings.json.
/// Atomic writes (tmp file + move), corruption recovery (rename to .bak, start with
/// defaults), and 200 ms debounced saves so dragging a slider does not thrash the disk.
/// </summary>
public sealed class SettingsService : IDisposable
{
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(200);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly object _saveGate = new();
    private Timer? _saveTimer;
    private bool _disposed;

    /// <summary>Raised synchronously on the caller's thread after every mutation.</summary>
    public event Action? SettingsChanged;

    public AppSettings Settings { get; }

    /// <summary>True when no settings file existed at load (first run opens settings once).</summary>
    public bool IsFirstRun { get; }

    public static string DefaultConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskFlip");

    public SettingsService(string configDirectory) : this(configDirectory, null)
    {
    }

    /// <param name="logicalScaleProvider">
    /// Returns the system DPI scale (physical/logical) used to migrate pre-v2 settings,
    /// which were stored in physical pixels. Null = query GetDpiForSystem.
    /// </param>
    public SettingsService(string configDirectory, Func<double>? logicalScaleProvider)
    {
        ConfigDirectory = configDirectory;
        _filePath = Path.Combine(configDirectory, "settings.json");
        (Settings, IsFirstRun) = Load(_filePath, logicalScaleProvider ?? QuerySystemScale);
    }

    /// <summary>Directory holding settings.json (and the diagnostic log).</summary>
    public string ConfigDirectory { get; }

    private static double QuerySystemScale()
    {
        var dpi = GetDpiForSystem();
        return dpi > 0 ? dpi / 96.0 : 1.0;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    /// <summary>Mutate settings, notify listeners, and schedule a debounced save.</summary>
    public void Update(Action<AppSettings> mutate)
    {
        lock (_saveGate)
        {
            mutate(Settings);
            Settings.Sanitize();
        }
        SettingsChanged?.Invoke();
        ScheduleSave();
    }

    /// <summary>Save immediately, cancelling any pending debounced write. Call on shutdown.</summary>
    public void Flush()
    {
        lock (_saveGate)
        {
            _saveTimer?.Dispose();
            _saveTimer = null;
            if (!_disposed)
                SaveNow();
        }
    }

    public void Dispose()
    {
        lock (_saveGate)
        {
            _disposed = true;
            _saveTimer?.Dispose();
            _saveTimer = null;
        }
    }

    private void ScheduleSave()
    {
        lock (_saveGate)
        {
            if (_disposed)
                return;
            _saveTimer ??= new Timer(_ => OnSaveTimer(), null, Timeout.Infinite, Timeout.Infinite);
            _saveTimer.Change(SaveDebounce, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnSaveTimer()
    {
        lock (_saveGate)
        {
            if (!_disposed)
                SaveNow();
        }
    }

    private void SaveNow()
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory); // idempotent

        Settings.SettingsVersion = AppSettings.CurrentSettingsVersion;
        var tmpPath = _filePath + ".tmp";
        var json = JsonSerializer.Serialize(Settings, JsonOptions);
        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, _filePath, overwrite: true);
    }

    private static (AppSettings Settings, bool IsFirstRun) Load(string filePath, Func<double> logicalScaleProvider)
    {
        if (!File.Exists(filePath))
            return (new AppSettings(), true);

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(filePath)) ?? new AppSettings();
            Migrate(settings, logicalScaleProvider());
            settings.Sanitize();
            return (settings, false);
        }
        catch (JsonException)
        {
            // Corrupt file: preserve it for forensics, start with defaults.
            TryBackup(filePath);
            return (new AppSettings(), false);
        }
        catch (IOException)
        {
            return (new AppSettings(), false);
        }
        catch (UnauthorizedAccessException)
        {
            return (new AppSettings(), false);
        }
    }

    /// <summary>
    /// v1 → v2: pixel parameters were stored in PHYSICAL pixels; v2 stores LOGICAL pixels
    /// (scale-independent). Divide by the system scale to preserve the exact
    /// physical behavior the user already tuned.
    /// </summary>
    private static void Migrate(AppSettings s, double systemScale)
    {
        if (s.SettingsVersion >= AppSettings.CurrentSettingsVersion)
            return;
        if (s.SettingsVersion < 2 && systemScale > 0)
        {
            static int ToLogical(int physical, double scale) => (int)Math.Round(physical / scale);
            s.TriggerWidth = ToLogical(s.TriggerWidth, systemScale);
            s.SegmentThreshold = ToLogical(s.SegmentThreshold, systemScale);
            s.HoldZoneWidth = ToLogical(s.HoldZoneWidth, systemScale);
            s.ReversalNoiseFloorPx = ToLogical(s.ReversalNoiseFloorPx, systemScale);
            s.JumpThresholdPx = ToLogical(s.JumpThresholdPx, systemScale);
        }
        s.SettingsVersion = AppSettings.CurrentSettingsVersion;
    }

    private static void TryBackup(string filePath)
    {
        try
        {
            File.Copy(filePath, filePath + ".bak", overwrite: true);
        }
        catch (IOException)
        {
            // Backup is best-effort; defaults still apply.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
