namespace DeskFlip.Gesture;

/// <summary>
/// Edge-rub gesture recognizer. Pure logic: no P/Invoke, no UI types.
/// Fed with <see cref="Feed"/> mouse-move samples; raises <see cref="SwitchRequested"/>
/// when N alternating vertical segments are completed inside an edge trigger zone.
///
/// Segment measurement is jitter-tolerant: progress is measured from the segment's
/// start point to its farthest excursion (extreme point), and only a retreat of at
/// least the reversal noise floor counts as a real reversal. Sub-noise wiggle
/// (±1–3 px, unavoidable with a human hand) neither accumulates nor resets anything.
/// All tunables live in <see cref="GestureParameters"/> and are hot-updatable.
/// </summary>
public sealed class GestureRecognizer
{
    private enum State { Idle, Armed }

    public event Action<SwitchDirection>? SwitchRequested;

    /// <summary>Human-readable state transitions, for the diagnostic log.</summary>
    public event Action<string>? Trace;

    private GestureParameters _p;

    private IReadOnlyList<EdgeSegment> _segments = Array.Empty<EdgeSegment>();

    private State _state = State.Idle;
    private EdgeSegment _armedSegment;
    private GesturePoint _lastPoint;
    private bool _hasLastPoint;
    private GestureButtons _lastButtons = GestureButtons.None;
    private DateTime _armedAt;
    private DateTime _lastSegmentCompletedAt;
    // Cooldown is a single slot: only the side that triggered most
    // recently cools down. A trigger on the other side REPLACES the active cooldown,
    // so fast left-right alternation always works; only same-side repeats are blocked.
    private EdgeSide _coolingSide;
    private DateTime _coolingUntil;
    private int _completedSegments;

    // Current stroke: direction, its start point, and its farthest excursion so far.
    private int _dir;            // +1 = down, -1 = up, 0 = no stroke in progress
    private int _segmentStartY;
    private int _extremeY;
    private bool _awaitingReversal; // a segment just completed; the next one starts on real reversal
    private bool _entryBlockLogged; // rate-limiter for the "entry blocked by button" trace
    private int _previousY;      // y of the previous sample while Armed
    private DateTime? _outsideHoldSince; // first sample outside the hold zone; null while inside

    public GestureRecognizer(GestureParameters parameters)
    {
        parameters.Validate();
        _p = parameters;
    }

    /// <summary>Hot-update any parameter: resets to Idle and re-evaluates the last known cursor position.</summary>
    public void UpdateParameters(GestureParameters parameters, DateTime now)
    {
        parameters.Validate();
        _p = parameters;
        Trace?.Invoke($"parameters updated: {parameters}");
        ResetToIdleAndReevaluate(now);
    }

    /// <summary>Hot-update the exposed edge map: resets to Idle and re-evaluates the last known cursor position.</summary>
    public void UpdateEdgeMap(IReadOnlyList<EdgeSegment> segments, DateTime now)
    {
        _segments = segments ?? throw new ArgumentNullException(nameof(segments));
        Trace?.Invoke($"edge map updated: {segments.Count} segment(s)");
        ResetToIdleAndReevaluate(now);
    }

    /// <summary>Feed one mouse-move sample. <paramref name="buttons"/> is the full held-button state at that moment.</summary>
    public void Feed(GesturePoint pt, GestureButtons buttons, DateTime now)
    {
        var jumped = _state == State.Armed && _hasLastPoint && IsJump(_lastPoint, pt);
        _lastPoint = pt;
        _hasLastPoint = true;
        _lastButtons = buttons;

        switch (_state)
        {
            case State.Idle:
                if (buttons == GestureButtons.None && FindSegment(pt) is { } seg && !IsCooling(seg.Side, now))
                {
                    Arm(seg, now);
                }
                else if (buttons != GestureButtons.None && FindSegment(pt) is { } blockedSeg && !_entryBlockLogged)
                {
                    // A stuck/held button (e.g. gaming-mouse side button) silently
                    // prevents arming — make it visible in the log, once per episode.
                    _entryBlockLogged = true;
                    Trace?.Invoke($"entry blocked: mouse button held ({buttons}) at {blockedSeg.Side} edge");
                }
                else if (buttons == GestureButtons.None)
                {
                    _entryBlockLogged = false;
                }
                break;

            case State.Armed:
                UpdateArmed(pt, buttons, now, jumped);
                break;
        }
    }

    private bool IsCooling(EdgeSide side, DateTime now) => side == _coolingSide && now < _coolingUntil;

    private void UpdateArmed(GesturePoint pt, GestureButtons buttons, DateTime now, bool jumped)
    {
        // Abandon conditions, checked before any accumulation.
        if (buttons != GestureButtons.None) { Abandon($"mouse button pressed (x={pt.X}, y={pt.Y})", now); return; }
        if (jumped) { Abandon($"position jump > {_p.JumpThresholdPx} px (x={pt.X}, y={pt.Y})", now); return; }
        if (FindSegment(pt) is { } other && other != _armedSegment) { Abandon($"edge segment changed (x={pt.X}, y={pt.Y})", now); return; }
        if (_completedSegments == 0 && now - _armedAt > FirstSegmentTimeout) { Abandon($"first-segment timeout ({_p.FirstSegmentTimeoutMs} ms)", now); return; }
        if (_completedSegments > 0 && now - _lastSegmentCompletedAt > InterSegmentTimeout) { Abandon($"inter-segment timeout ({_p.InterSegmentTimeoutMs} ms)", now); return; }

        // Hold zone with a grace period: brief excursions are normal hand drift, only a
        // continuous stay outside abandons the gesture. Movement while OUTSIDE never
        // counts toward strokes — the grace keeps the gesture alive, not productive.
        if (IsInsideHoldZone(pt))
        {
            _outsideHoldSince = null;
        }
        else
        {
            if (_outsideHoldSince == null)
            {
                _outsideHoldSince = now;
            }
            else if (now - _outsideHoldSince.Value >= HoldZoneGrace)
            {
                Abandon($"outside hold zone for {_p.HoldZoneGraceMs} ms (x={pt.X}, y={pt.Y})", now);
            }
            return;
        }

        var y = pt.Y;
        if (_dir == 0)
        {
            var dy = y - _previousY;
            if (dy == 0)
                return;
            _previousY = y;
            _dir = Math.Sign(dy);
            _segmentStartY = y - dy;
            _extremeY = y;
            CheckProgress(now);
            return;
        }

        if (_dir > 0)
        {
            if (y >= _extremeY)
            {
                _extremeY = y;
                if (!_awaitingReversal)
                    CheckProgress(now);
            }
            else if (_extremeY - y >= NoiseFloorPx)
            {
                Reverse(y, now);
            }
            // else: sub-noise retreat — jitter, ignored.
        }
        else
        {
            if (y <= _extremeY)
            {
                _extremeY = y;
                if (!_awaitingReversal)
                    CheckProgress(now);
            }
            else if (y - _extremeY >= NoiseFloorPx)
            {
                Reverse(y, now);
            }
        }
    }

    private TimeSpan FirstSegmentTimeout => TimeSpan.FromMilliseconds(_p.FirstSegmentTimeoutMs);
    private TimeSpan InterSegmentTimeout => TimeSpan.FromMilliseconds(_p.InterSegmentTimeoutMs);
    private TimeSpan HoldZoneGrace => TimeSpan.FromMilliseconds(_p.HoldZoneGraceMs);

    /// <summary>Real reversal at the extreme point: the old (incomplete) accumulation is discarded and a new stroke starts from the turning point.</summary>
    private void Reverse(int y, DateTime now)
    {
        _dir = -_dir;
        _segmentStartY = _extremeY;
        _extremeY = y;
        _awaitingReversal = false;
        CheckProgress(now);
    }

    private int Progress => _dir > 0 ? _extremeY - _segmentStartY : _segmentStartY - _extremeY;

    // All pixel parameters are logical; the armed segment's monitor scale converts them
    // to physical pixels, so a parameter feels identical on any DPI.
    private double SegmentThresholdPx => _p.SegmentThreshold * _armedSegment.Scale;
    private double NoiseFloorPx => _p.ReversalNoiseFloorPx * _armedSegment.Scale;
    private double JumpThresholdPx => _p.JumpThresholdPx * _armedSegment.Scale;

    private void CheckProgress(DateTime now)
    {
        if (Progress < SegmentThresholdPx)
            return;

        // Segment completed: surplus displacement is discarded. Continued
        // motion in the same direction only extends the extreme point; the next segment
        // starts on a real reversal, so one long swipe can never count as two segments.
        _completedSegments++;
        _awaitingReversal = true;
        _segmentStartY = _extremeY;
        _lastSegmentCompletedAt = now;
        Trace?.Invoke($"segment {_completedSegments}/{_p.SegmentCount} complete");

        if (_completedSegments >= _p.SegmentCount)
        {
            var side = _armedSegment.Side;
            var direction = side == EdgeSide.Left ? SwitchDirection.Left : SwitchDirection.Right;
            // Only this side cools down now; any cooldown on the other side is cancelled.
            _coolingSide = side;
            _coolingUntil = now + TimeSpan.FromMilliseconds(_p.CooldownMs);
            ClearProgress();
            Trace?.Invoke($"TRIGGER {direction} (cooling {side} for {_p.CooldownMs} ms)");
            ResetToIdleAndReevaluate(now);
            SwitchRequested?.Invoke(direction);
        }
    }

    private void Arm(EdgeSegment seg, DateTime now)
    {
        _state = State.Armed;
        _armedSegment = seg;
        _armedAt = now;
        ClearProgress();
        _previousY = _lastPoint.Y;
        Trace?.Invoke($"armed: {seg.Side} edge x={seg.X} y=[{seg.Top},{seg.Bottom})");
    }

    private void Abandon(string reason, DateTime now)
    {
        Trace?.Invoke($"abandoned: {reason}");
        ResetToIdleAndReevaluate(now);
    }

    private void ClearProgress()
    {
        _completedSegments = 0;
        _dir = 0;
        _awaitingReversal = false;
        _outsideHoldSince = null;
    }

    private void ResetToIdleAndReevaluate(DateTime now)
    {
        _state = State.Idle;
        ClearProgress();
        // Re-evaluate the current cursor position instead of waiting for an "enter edge"
        // event. The button rule still applies: while a
        // button is held (e.g. dragging a scrollbar at the edge) we must not re-arm.
        // A side in post-trigger cooldown does not re-arm until its cooldown expires.
        if (_hasLastPoint && _lastButtons == GestureButtons.None
            && FindSegment(_lastPoint) is { } seg && !IsCooling(seg.Side, now))
            Arm(seg, now);
    }

    private EdgeSegment? FindSegment(GesturePoint pt)
    {
        foreach (var seg in _segments)
        {
            if (!seg.ContainsY(pt.Y))
                continue;
            var width = _p.TriggerWidth * seg.Scale;
            // Exposed edges have no neighbor outside; samples slightly beyond the edge
            // (clamped-cursor reports like x=-3 or x=2565) are still "at the edge".
            var inZone = seg.Side == EdgeSide.Left
                ? pt.X < seg.X + width
                : pt.X >= seg.X - width;
            if (inZone)
                return seg;
        }
        return null;
    }

    private bool IsInsideHoldZone(GesturePoint pt)
    {
        if (!_armedSegment.ContainsY(pt.Y))
            return false;
        // The hold zone is never narrower than the trigger zone; like the trigger zone
        // it extends unbounded outward (beyond-edge samples count as at the edge).
        var width = Math.Max(_p.HoldZoneWidth, _p.TriggerWidth) * _armedSegment.Scale;
        return _armedSegment.Side == EdgeSide.Left
            ? pt.X < _armedSegment.X + width
            : pt.X >= _armedSegment.X - width;
    }

    private bool IsJump(GesturePoint a, GesturePoint b) =>
        Math.Abs(b.X - a.X) > JumpThresholdPx || Math.Abs(b.Y - a.Y) > JumpThresholdPx;
}
