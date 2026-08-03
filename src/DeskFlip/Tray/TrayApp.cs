using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DeskFlip.Config;
using DeskFlip.Display;
using DeskFlip.Gesture;
using DeskFlip.Guard;
using DeskFlip.Hook;
using DeskFlip.Properties;
using DeskFlip.Switch;
using Hardcodet.Wpf.TaskbarNotification;
using Strings = DeskFlip.Properties.Strings;

namespace DeskFlip.Tray;

/// <summary>
/// Composition root and tray host. Wires the hook → recognizer → switcher
/// pipeline, enforces the master enable gate (not paused, not fullscreen-suppressed, not
/// blocklist-suppressed), and owns the tray icon, its menu, and the settings window.
/// All recognizer interaction happens on the UI thread; background events are marshaled.
/// </summary>
public sealed class TrayApp : IDisposable
{
    private readonly SettingsService _settings;
    private readonly AutostartService _autostart;
    private readonly SingleInstanceGuard _singleInstance;
    private readonly Dispatcher _dispatcher;

    private readonly GestureRecognizer _recognizer;
    private readonly IMouseEventSource _hook;
    private readonly DesktopSwitcher _switcher;
    private readonly EdgeMapService _edgeMap;
    private readonly FullscreenGuard _fullscreenGuard;
    private readonly BlocklistMonitor _blocklist;

    private TaskbarIcon? _trayIcon;
    private MenuItem? _pauseMenuItem;
    private System.Drawing.Icon? _iconNormal;
    private System.Drawing.Icon? _iconGray;
    private SettingsWindow? _settingsWindow;

    private GestureParameters _appliedParameters;

    public TrayApp(SettingsService settings, AutostartService autostart, SingleInstanceGuard singleInstance)
        : this(settings, autostart, singleInstance, new MouseHookService())
    {
    }

    /// <summary>Test-friendly constructor with an injectable mouse source.</summary>
    public TrayApp(SettingsService settings, AutostartService autostart, SingleInstanceGuard singleInstance,
        IMouseEventSource mouseSource)
    {
        _settings = settings;
        _autostart = autostart;
        _singleInstance = singleInstance;
        _dispatcher = Application.Current.Dispatcher;

        Log.Init(settings.ConfigDirectory);
        Log.Enabled = settings.Settings.EnableLogging;

        var s = settings.Settings;
        _recognizer = new GestureRecognizer(s.ToGestureParameters());
        _appliedParameters = s.ToGestureParameters();
        _recognizer.SwitchRequested += OnSwitchRequested;
        _recognizer.Trace += msg => Log.Write($"gesture: {msg}");

        _hook = mouseSource;
        _hook.MouseMoved += OnMouseMoved;
        _hook.Trace += msg => Log.Write($"hook: {msg}");

        _switcher = new DesktopSwitcher();

        _edgeMap = new EdgeMapService();
        _recognizer.UpdateEdgeMap(_edgeMap.Segments, DateTime.Now);
        _edgeMap.EdgeMapChanged += OnEdgeMapChanged;

        _fullscreenGuard = new FullscreenGuard();
        _fullscreenGuard.FullscreenChanged += fs =>
        {
            Log.Write($"guard: fullscreen -> {fs}");
            MarshalToUi(RefreshGate);
        };

        _blocklist = new BlocklistMonitor();
        _blocklist.SetBlockedNames(s.BlockedProcesses);
        _blocklist.BlockedChanged += b =>
        {
            Log.Write($"guard: blocklist -> {b}");
            MarshalToUi(RefreshGate);
        };

        _settings.SettingsChanged += ApplySettings;
        _singleInstance.ShowRequested += () => MarshalToUi(ShowSettings);
    }

    public void Start()
    {
        CreateTrayIcon();
        ApplySettings();
        if (_settings.Settings.DisableWhenFullscreen)
            _fullscreenGuard.Start();
        _blocklist.Start();
        _hook.Start();

        if (_settings.IsFirstRun)
            ShowSettings(); // first run: teach the gesture once
    }

    // Master gate: hook events are dropped entirely while suppressed.
    private bool GesturesEnabled =>
        !_settings.Settings.Paused
        && !(_settings.Settings.DisableWhenFullscreen && _fullscreenGuard.IsFullscreen)
        && !_blocklist.IsBlocked;

    private void OnMouseMoved(GesturePoint pt, GestureButtons buttons, DateTime at)
    {
        if (!GesturesEnabled)
        {
            LogGateClosureOnce();
            return;
        }
        MarshalToUi(() =>
        {
            if (GesturesEnabled)
                _recognizer.Feed(pt, buttons, at);
        });
    }

    // Suppressed events are dropped silently per design, but the reason belongs in the log
    // (rate-limited: one line per suppression episode, not per mouse sample).
    private string _lastGateClosure = string.Empty;

    private void LogGateClosureOnce()
    {
        var s = _settings.Settings;
        var reason = s.Paused ? "paused"
            : (s.DisableWhenFullscreen && _fullscreenGuard.IsFullscreen) ? "fullscreen"
            : _blocklist.IsBlocked ? "blocklist"
            : string.Empty;
        if (reason.Length == 0 || reason == _lastGateClosure)
            return;
        _lastGateClosure = reason;
        Log.Write($"gate: closed ({reason}) — mouse events dropped");
    }

    private void OnSwitchRequested(SwitchDirection direction)
    {
        // Endpoints do not wrap: the OS shortcut is a silent no-op there.
        var injected = _switcher.Switch(direction);
        Log.Write($"switch: {direction} injected={injected}");
    }

    private void OnEdgeMapChanged(IReadOnlyList<EdgeSegment> segments) =>
        MarshalToUi(() => _recognizer.UpdateEdgeMap(segments, DateTime.Now));

    private void ApplySettings()
    {
        var s = _settings.Settings;
        var parameters = s.ToGestureParameters();
        if (!parameters.Equals(_appliedParameters))
        {
            _recognizer.UpdateParameters(parameters, DateTime.Now);
            _appliedParameters = parameters;
        }
        _blocklist.SetBlockedNames(s.BlockedProcesses);
        if (s.EnableLogging != Log.Enabled)
        {
            Log.Enabled = s.EnableLogging;
            if (s.EnableLogging)
                Log.Write("logging enabled");
        }
        if (s.DisableWhenFullscreen)
            _fullscreenGuard.Start();
        else
            _fullscreenGuard.Stop();
        if (_pauseMenuItem != null)
            _pauseMenuItem.IsChecked = s.Paused;
        RefreshGate();
    }

    private void RefreshGate()
    {
        if (_trayIcon == null)
            return;
        var s = _settings.Settings;
        _trayIcon.ToolTipText = s.Paused ? Strings.App_TrayTooltip_Paused
            : (s.DisableWhenFullscreen && _fullscreenGuard.IsFullscreen) ? Strings.App_TrayTooltip_Fullscreen
            : _blocklist.IsBlocked ? Strings.App_TrayTooltip_Blocked
            : Strings.App_TrayTooltip_Running;
        _trayIcon.Icon = s.Paused ? _iconGray : _iconNormal; // gray while paused
        if (GesturesEnabled)
            _lastGateClosure = string.Empty; // next suppression episode logs again
    }

    private void CreateTrayIcon()
    {
        _iconNormal = LoadIcon("Assets/tray.ico");
        _iconGray = LoadIcon("Assets/tray-gray.ico");

        var menu = new ContextMenu();
        var openItem = new MenuItem { Header = Strings.Menu_OpenSettings };
        openItem.Click += (_, _) => ShowSettings();
        _pauseMenuItem = new MenuItem { Header = Strings.Menu_Pause, IsCheckable = true };
        _pauseMenuItem.Click += (_, _) => _settings.Update(s => s.Paused = !s.Paused);
        var logItem = new MenuItem { Header = Strings.Menu_OpenLog };
        logItem.Click += (_, _) => OpenLog();
        var exitItem = new MenuItem { Header = Strings.Menu_Exit };
        exitItem.Click += (_, _) => Exit();
        menu.Items.Add(openItem);
        menu.Items.Add(_pauseMenuItem);
        menu.Items.Add(logItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exitItem);

        _trayIcon = new TaskbarIcon
        {
            Icon = _iconNormal,
            ContextMenu = menu,
        };
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowSettings();
        RefreshGate();
    }

    public void ShowSettings()
    {
        if (_settingsWindow != null)
        {
            if (_settingsWindow.Visibility != Visibility.Visible)
                _settingsWindow.Show(); // was minimized to tray
            if (_settingsWindow.WindowState == WindowState.Minimized)
                _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(_settings, _autostart, Exit, _edgeMap);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private static void OpenLog()
    {
        if (Log.FilePath.Length == 0)
            return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Log.FilePath)
        {
            UseShellExecute = true,
        });
    }

    private void Exit()
    {
        // Only explicit exits terminate the process; closing the settings window never
        // does (ShutdownMode=OnExplicitShutdown).
        if (_settingsWindow != null)
            _settingsWindow.AllowClose = true;
        Application.Current.Shutdown();
    }

    private void MarshalToUi(Action action)
    {
        if (_dispatcher.CheckAccess())
            action();
        else
            _dispatcher.BeginInvoke(action);
    }

    private static System.Drawing.Icon LoadIcon(string relativePath)
    {
        var stream = Application.GetResourceStream(new Uri(relativePath, UriKind.Relative))!.Stream;
        return new System.Drawing.Icon(stream);
    }

    public void Dispose()
    {
        _settings.Flush();
        _hook.MouseMoved -= OnMouseMoved;
        _hook.Stop();
        (_hook as IDisposable)?.Dispose();
        _fullscreenGuard.Dispose();
        _blocklist.Dispose();
        _edgeMap.Dispose();
        _trayIcon?.Dispose();
        _iconNormal?.Dispose();
        _iconGray?.Dispose();
        _settings.Dispose();
        Log.Close();
    }
}
