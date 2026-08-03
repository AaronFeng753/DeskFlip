using System.Globalization;
using System.Resources;

namespace DeskFlip.Properties;

/// <summary>
/// Hand-written strongly-typed accessor over the .resx pair (CLI builds do not run
/// ResXFileCodeGenerator). Lookup follows <see cref="CultureInfo.CurrentUICulture"/>,
/// which App sets at startup from the Language setting (restart-required).
/// Named Strings (not Resources) to avoid colliding with FrameworkElement.Resources.
/// </summary>
internal static class Strings
{
    private static readonly ResourceManager Manager =
        new("DeskFlip.Properties.Resources", typeof(Strings).Assembly);

    private static string Get(string name) => Manager.GetString(name, CultureInfo.CurrentUICulture) ?? name;

    public static string App_TrayTooltip_Running => Get(nameof(App_TrayTooltip_Running));
    public static string App_TrayTooltip_Paused => Get(nameof(App_TrayTooltip_Paused));
    public static string App_TrayTooltip_Fullscreen => Get(nameof(App_TrayTooltip_Fullscreen));
    public static string App_TrayTooltip_Blocked => Get(nameof(App_TrayTooltip_Blocked));
    public static string Menu_OpenSettings => Get(nameof(Menu_OpenSettings));
    public static string Menu_Pause => Get(nameof(Menu_Pause));
    public static string Menu_OpenLog => Get(nameof(Menu_OpenLog));
    public static string Menu_Exit => Get(nameof(Menu_Exit));
    public static string Settings_Title => Get(nameof(Settings_Title));
    public static string Settings_GestureHelp => Get(nameof(Settings_GestureHelp));
    public static string Settings_TriggerWidth => Get(nameof(Settings_TriggerWidth));
    public static string Settings_Sensitivity => Get(nameof(Settings_Sensitivity));
    public static string Settings_RubCount => Get(nameof(Settings_RubCount));
    public static string Settings_RubCountOneWarning => Get(nameof(Settings_RubCountOneWarning));
    public static string Settings_HighDpiHint => Get(nameof(Settings_HighDpiHint));
    public static string Settings_DisableFullscreen => Get(nameof(Settings_DisableFullscreen));
    public static string Settings_Blocklist => Get(nameof(Settings_Blocklist));
    public static string Settings_BlocklistAdd => Get(nameof(Settings_BlocklistAdd));
    public static string Settings_BlocklistRemove => Get(nameof(Settings_BlocklistRemove));
    public static string Settings_AutoStart => Get(nameof(Settings_AutoStart));
    public static string Settings_Paused => Get(nameof(Settings_Paused));
    public static string Settings_Language => Get(nameof(Settings_Language));
    public static string Settings_LanguageSystem => Get(nameof(Settings_LanguageSystem));
    public static string Settings_LanguageRestartNotice => Get(nameof(Settings_LanguageRestartNotice));
    public static string Settings_AutostartDenied => Get(nameof(Settings_AutostartDenied));
    public static string Settings_EnableLogging => Get(nameof(Settings_EnableLogging));
    public static string Settings_ZoneVisualization => Get(nameof(Settings_ZoneVisualization));
    public static string Settings_OpenDataFolder => Get(nameof(Settings_OpenDataFolder));
    public static string Settings_MinimizeToTray => Get(nameof(Settings_MinimizeToTray));
    public static string Settings_AdvancedHeader => Get(nameof(Settings_AdvancedHeader));
    public static string Settings_HoldZoneWidth => Get(nameof(Settings_HoldZoneWidth));
    public static string Settings_HoldZoneGrace => Get(nameof(Settings_HoldZoneGrace));
    public static string Settings_NoiseFloor => Get(nameof(Settings_NoiseFloor));
    public static string Settings_FirstSegmentTimeout => Get(nameof(Settings_FirstSegmentTimeout));
    public static string Settings_InterSegmentTimeout => Get(nameof(Settings_InterSegmentTimeout));
    public static string Settings_Cooldown => Get(nameof(Settings_Cooldown));
    public static string Settings_JumpThreshold => Get(nameof(Settings_JumpThreshold));
    public static string Settings_ResetDefaults => Get(nameof(Settings_ResetDefaults));
    public static string Settings_ResetConfirm => Get(nameof(Settings_ResetConfirm));
    public static string Settings_BasicHeader => Get(nameof(Settings_BasicHeader));
    public static string Settings_BehaviorHeader => Get(nameof(Settings_BehaviorHeader));
    public static string Tip_TriggerWidth => Get(nameof(Tip_TriggerWidth));
    public static string Tip_Sensitivity => Get(nameof(Tip_Sensitivity));
    public static string Tip_RubCount => Get(nameof(Tip_RubCount));
    public static string Tip_HoldZoneWidth => Get(nameof(Tip_HoldZoneWidth));
    public static string Tip_HoldZoneGrace => Get(nameof(Tip_HoldZoneGrace));
    public static string Tip_NoiseFloor => Get(nameof(Tip_NoiseFloor));
    public static string Tip_FirstSegmentTimeout => Get(nameof(Tip_FirstSegmentTimeout));
    public static string Tip_InterSegmentTimeout => Get(nameof(Tip_InterSegmentTimeout));
    public static string Tip_Cooldown => Get(nameof(Tip_Cooldown));
    public static string Tip_JumpThreshold => Get(nameof(Tip_JumpThreshold));
    public static string Tip_DisableFullscreen => Get(nameof(Tip_DisableFullscreen));
    public static string Tip_Blocklist => Get(nameof(Tip_Blocklist));
    public static string Tip_AutoStart => Get(nameof(Tip_AutoStart));
    public static string Tip_EnableLogging => Get(nameof(Tip_EnableLogging));
    public static string Tip_ZoneVisualization => Get(nameof(Tip_ZoneVisualization));
    public static string Tip_Paused => Get(nameof(Tip_Paused));
    public static string Tip_Language => Get(nameof(Tip_Language));
    public static string Tip_Promo => Get(nameof(Tip_Promo));
    public static string Tip_GitHub => Get(nameof(Tip_GitHub));
    public static string Settings_About => Get(nameof(Settings_About));
    public static string About_Title => Get(nameof(About_Title));
    public static string About_Disclaimer => Get(nameof(About_Disclaimer));
    public static string About_License => Get(nameof(About_License));
    public static string Common_Yes => Get(nameof(Common_Yes));
    public static string Common_No => Get(nameof(Common_No));
    public static string Common_OK => Get(nameof(Common_OK));

    /// <summary>Number of promo taglines (Promo_1..Promo_N) in the resx files.</summary>
    public const int PromoTaglineCount = 12;

    /// <summary>Promo tagline for the Waifu2x-Extension-GUI button; 0-based
    /// index into Promo_1..Promo_N.</summary>
    public static string PromoTagline(int index) => Get($"Promo_{index + 1}");
}
