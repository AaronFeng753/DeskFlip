namespace DeskFlip;

/// <summary>
/// Build version: date + same-day revision, <c>YYMMDD-RevN</c> (e.g. 260803-Rev1).
/// Bump Rev on every publish; reset to Rev1 when the date rolls over.
/// </summary>
public static class AppVersion
{
    public const string Current = "260803-Rev5";
}
