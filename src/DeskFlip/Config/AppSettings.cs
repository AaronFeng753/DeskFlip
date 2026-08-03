using DeskFlip.Gesture;

namespace DeskFlip.Config;

/// <summary>
/// Persisted user settings. Pixel values are LOGICAL pixels (96-DPI units,
/// scale-independent); times are milliseconds.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Follow-system marker for <see cref="Language"/>.</summary>
    public const string LanguageSystem = "system";

    /// <summary>Current on-disk format version (2 = logical pixels; 1 = physical pixels).</summary>
    public const int CurrentSettingsVersion = 2;

    /// <summary>Format version of the loaded file; missing (0) is treated as v1 and migrated.
    /// Stamped to <see cref="CurrentSettingsVersion"/> by SettingsService on every save.</summary>
    public int SettingsVersion { get; set; }

    // Basic gesture parameters (defaults and ranges mirror GestureParameters).
    public int TriggerWidth { get; set; } = 4;
    public int SegmentThreshold { get; set; } = 45;
    public int SegmentCount { get; set; } = 3;

    // Advanced gesture parameters.
    public int HoldZoneWidth { get; set; } = 8;
    public int HoldZoneGraceMs { get; set; } = 300;
    public int ReversalNoiseFloorPx { get; set; } = 9;
    public int FirstSegmentTimeoutMs { get; set; } = 2000;
    public int InterSegmentTimeoutMs { get; set; } = 1000;
    public int CooldownMs { get; set; } = 1000;
    public int JumpThresholdPx { get; set; } = 300;

    public bool DisableWhenFullscreen { get; set; } = true;
    public List<string> BlockedProcesses { get; set; } = new();
    public bool AutoStart { get; set; }
    public bool Paused { get; set; }

    /// <summary>Diagnostic log master switch, off by default.</summary>
    public bool EnableLogging { get; set; }

    /// <summary>Semi-transparent overlay visualizing trigger/hold zones while the settings
    /// window is open. On by default.</summary>
    public bool ShowZoneVisualization { get; set; } = true;

    /// <summary>"system" (follow OS), "zh-CN" or "en". Takes effect after restart.</summary>
    public string Language { get; set; } = LanguageSystem;

    /// <summary>Gesture-recognizer view of the settings.</summary>
    public GestureParameters ToGestureParameters() => new(
        TriggerWidth, SegmentThreshold, SegmentCount,
        HoldZoneWidth, HoldZoneGraceMs, ReversalNoiseFloorPx,
        FirstSegmentTimeoutMs, InterSegmentTimeoutMs, CooldownMs, JumpThresholdPx);

    /// <summary>Assigns every setting from <paramref name="other"/> (used by "restore defaults").</summary>
    public void CopyFrom(AppSettings other)
    {
        TriggerWidth = other.TriggerWidth;
        SegmentThreshold = other.SegmentThreshold;
        SegmentCount = other.SegmentCount;
        HoldZoneWidth = other.HoldZoneWidth;
        HoldZoneGraceMs = other.HoldZoneGraceMs;
        ReversalNoiseFloorPx = other.ReversalNoiseFloorPx;
        FirstSegmentTimeoutMs = other.FirstSegmentTimeoutMs;
        InterSegmentTimeoutMs = other.InterSegmentTimeoutMs;
        CooldownMs = other.CooldownMs;
        JumpThresholdPx = other.JumpThresholdPx;
        DisableWhenFullscreen = other.DisableWhenFullscreen;
        BlockedProcesses = new List<string>(other.BlockedProcesses);
        AutoStart = other.AutoStart;
        Paused = other.Paused;
        EnableLogging = other.EnableLogging;
        ShowZoneVisualization = other.ShowZoneVisualization;
        Language = other.Language;
    }

    /// <summary>
    /// Enforces the hard algorithmic invariants (see <see cref="GestureParameters.Validate"/>);
    /// typed-in values beyond the slider ranges are preserved by design.
    /// </summary>
    public void Sanitize()
    {
        TriggerWidth = Math.Max(TriggerWidth, 1);
        // Threshold must be ≥ 2 so a noise floor of 1 can stay below it.
        SegmentThreshold = Math.Max(SegmentThreshold, 2);
        SegmentCount = Math.Max(SegmentCount, 1);
        HoldZoneWidth = Math.Max(HoldZoneWidth, 1);
        HoldZoneGraceMs = Math.Max(HoldZoneGraceMs, 0);
        ReversalNoiseFloorPx = Math.Clamp(ReversalNoiseFloorPx, 1, SegmentThreshold - 1);
        FirstSegmentTimeoutMs = Math.Max(FirstSegmentTimeoutMs, 1);
        InterSegmentTimeoutMs = Math.Max(InterSegmentTimeoutMs, 1);
        CooldownMs = Math.Max(CooldownMs, 0);
        JumpThresholdPx = Math.Max(JumpThresholdPx, 1);
        BlockedProcesses = BlockedProcesses
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (Language is not (LanguageSystem or "zh-CN" or "en"))
            Language = LanguageSystem;
    }
}
