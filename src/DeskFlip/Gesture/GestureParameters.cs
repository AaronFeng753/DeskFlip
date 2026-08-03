namespace DeskFlip.Gesture;

/// <summary>
/// Every tunable of the gesture recognizer. Lengths are LOGICAL pixels
/// (96-DPI units, resolution/scale-independent); each edge segment carries its monitor's
/// scale and the recognizer converts per segment. Times are milliseconds.
/// Immutable; hot-updates replace the whole value.
/// </summary>
public readonly record struct GestureParameters(
    int TriggerWidth,          // W: edge trigger-zone width
    int SegmentThreshold,      // T: vertical displacement of one stroke
    int SegmentCount,          // N: strokes needed to trigger
    int HoldZoneWidth,         // horizontal tolerance while armed
    int HoldZoneGraceMs,       // continuous time outside the hold zone before abandoning
    int ReversalNoiseFloorPx,  // retreat from the extreme that counts as a real reversal
    int FirstSegmentTimeoutMs, // budget for the first stroke after arming
    int InterSegmentTimeoutMs, // budget between consecutive strokes
    int CooldownMs,            // global cooldown after a trigger
    int JumpThresholdPx)       // single-sample displacement that resets the gesture
{
    public const int MinTriggerWidth = 1;
    public const int MaxTriggerWidth = 50;
    public const int MinSegmentThreshold = 40;
    public const int MaxSegmentThreshold = 120;
    public const int MinSegmentCount = 1;
    public const int MaxSegmentCount = 5;
    public const int MinHoldZoneWidth = 5;
    public const int MaxHoldZoneWidth = 250;
    public const int MinHoldZoneGraceMs = 0;
    public const int MaxHoldZoneGraceMs = 2000;
    public const int MinReversalNoiseFloorPx = 2;
    public const int MaxReversalNoiseFloorPx = 40;
    public const int MinFirstSegmentTimeoutMs = 500;
    public const int MaxFirstSegmentTimeoutMs = 10000;
    public const int MinInterSegmentTimeoutMs = 200;
    public const int MaxInterSegmentTimeoutMs = 5000;
    public const int MinCooldownMs = 0;
    public const int MaxCooldownMs = 2000;
    public const int MinJumpThresholdPx = 50;
    public const int MaxJumpThresholdPx = 1000;

    public static GestureParameters Default => new(
        TriggerWidth: 4,
        SegmentThreshold: 45,
        SegmentCount: 3,
        HoldZoneWidth: 8,
        HoldZoneGraceMs: 300,
        ReversalNoiseFloorPx: 9,
        FirstSegmentTimeoutMs: 2000,
        InterSegmentTimeoutMs: 1000,
        CooldownMs: 1000,
        JumpThresholdPx: 300);

    /// <summary>
    /// Hard algorithmic invariants only (positivity, noise floor below T). The Min*/Max*
    /// constants are SLIDER ranges for the settings GUI; values typed in directly may
    /// exceed them and still be valid.
    /// </summary>
    public void Validate()
    {
        if (TriggerWidth < 1)
            throw new ArgumentOutOfRangeException(nameof(TriggerWidth));
        if (SegmentThreshold < 1)
            throw new ArgumentOutOfRangeException(nameof(SegmentThreshold));
        if (SegmentCount < 1)
            throw new ArgumentOutOfRangeException(nameof(SegmentCount));
        if (HoldZoneWidth < 1)
            throw new ArgumentOutOfRangeException(nameof(HoldZoneWidth));
        if (HoldZoneGraceMs < 0)
            throw new ArgumentOutOfRangeException(nameof(HoldZoneGraceMs));
        if (ReversalNoiseFloorPx < 1)
            throw new ArgumentOutOfRangeException(nameof(ReversalNoiseFloorPx));
        if (ReversalNoiseFloorPx >= SegmentThreshold)
            throw new ArgumentOutOfRangeException(nameof(ReversalNoiseFloorPx),
                "Reversal noise floor must be smaller than the segment threshold.");
        if (FirstSegmentTimeoutMs < 1)
            throw new ArgumentOutOfRangeException(nameof(FirstSegmentTimeoutMs));
        if (InterSegmentTimeoutMs < 1)
            throw new ArgumentOutOfRangeException(nameof(InterSegmentTimeoutMs));
        if (CooldownMs < 0)
            throw new ArgumentOutOfRangeException(nameof(CooldownMs));
        if (JumpThresholdPx < 1)
            throw new ArgumentOutOfRangeException(nameof(JumpThresholdPx));
    }
}
