using System.Globalization;
using System.Windows;
using DeskFlip.Config;
using DeskFlip.Tray;

namespace DeskFlip;

public partial class App : Application
{
    private SingleInstanceGuard? _singleInstance;
    private TrayApp? _trayApp;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstanceGuard();
        if (!_singleInstance.TryAcquire())
        {
            // Another instance is running; it has been asked to show its settings.
            Shutdown();
            return;
        }

        var settings = new SettingsService(SettingsService.DefaultConfigDirectory);
        ApplyLanguage(settings.Settings.Language);

        var autostart = new AutostartService();
        autostart.SelfHeal();

        _trayApp = new TrayApp(settings, autostart, _singleInstance);
        _trayApp.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayApp?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Language override applies before any UI is created; changes need a restart.</summary>
    private static void ApplyLanguage(string language)
    {
        if (language == AppSettings.LanguageSystem)
            return;
        var culture = new CultureInfo(language);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
