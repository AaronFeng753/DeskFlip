using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DeskFlip.Config;
using DeskFlip.Display;
using DeskFlip.Gesture;
using DeskFlip.Properties;
using Strings = DeskFlip.Properties.Strings;

namespace DeskFlip.Tray;

/// <summary>
/// Settings window. Every setting except language applies immediately;
/// writes go through <see cref="SettingsService.Update"/>, which debounces disk saves.
/// Closing this window never exits the process (ShutdownMode=OnExplicitShutdown).
///
/// Every numeric parameter has a slider (bounded) and a text box (unbounded except the
/// hard algorithmic invariants enforced by <see cref="AppSettings.Sanitize"/>); every
/// control carries a tooltip explaining what the parameter does.
/// </summary>
public partial class SettingsWindow : Window
{
    private sealed record LanguageOption(string Id, string Label)
    {
        // ToString is the ComboBox selection-box fallback when no item template resolves.
        public override string ToString() => Label;
    }
    private sealed record ParamRow(Slider Slider, TextBox Box, Func<AppSettings, int> Get);

    // Picked once per process: every app start shows a different promo line.
    private static readonly int PromoIndex = Random.Shared.Next(Strings.PromoTaglineCount);

    private const string PromoUrl = "https://github.com/AaronFeng753/Waifu2x-Extension-GUI/releases/latest";
    private const string GitHubUrl = "https://github.com/AaronFeng753";
    private const string ReleasesUrl = "https://github.com/AaronFeng753/DeskFlip/releases";

    private readonly SettingsService _settings;
    private readonly AutostartService _autostart;
    private readonly Action _exitAction;
    private readonly EdgeMapService _edgeMap;
    private readonly ZoneVisualizer _visualizer = new();
    private readonly string _languageAtOpen;
    private bool _loading;

    /// <summary>Set by TrayApp on real shutdown; until then every close path minimizes to tray.</summary>
    public bool AllowClose { get; set; }

    private readonly List<ParamRow> _paramRows = new();
    private TextBlock? _rubCountWarning;
    private CheckBox? _disableFullscreenCheck;
    private CheckBox? _autoStartCheck;
    private CheckBox? _pausedCheck;
    private CheckBox? _enableLoggingCheck;
    private CheckBox? _zoneVisualizationCheck;
    private ListBox? _blocklistBox;
    private TextBox? _blocklistInput;
    private ComboBox? _languageCombo;
    private TextBlock? _languageRestartNotice;

    public SettingsWindow(SettingsService settings, AutostartService autostart, Action exitAction,
        EdgeMapService edgeMap)
    {
        _settings = settings;
        _autostart = autostart;
        _exitAction = exitAction;
        _edgeMap = edgeMap;
        _languageAtOpen = settings.Settings.Language;

        InitializeComponent();
        DarkTitleBar.Apply(this); // dark non-client frame; no white flashbang
        Title = $"{Strings.Settings_Title} — by Aaron Feng";
        GestureHelpText.Text = Strings.Settings_GestureHelp;
        ResetButton.Content = Strings.Settings_ResetDefaults;
        MinimizeButton.Content = Strings.Settings_MinimizeToTray;
        ExitButton.Content = Strings.Menu_Exit;
        OpenDataFolderButton.Content = Strings.Settings_OpenDataFolder;
        GitHubButton.Content = "GitHub";
        GitHubButton.ToolTip = Strings.Tip_GitHub;
        AboutButton.Content = Strings.Settings_About;
        PromoButton.Content = Strings.PromoTagline(PromoIndex);
        PromoButton.ToolTip = Strings.Tip_Promo;

        BuildGesturePanel();
        BuildBehaviorPanel();
        BuildAttribution();
        LoadState();

        ResetButton.Click += (_, _) => OnResetDefaults();
        MinimizeButton.Click += (_, _) => Hide();
        ExitButton.Click += (_, _) =>
        {
            AllowClose = true;
            _exitAction();
        };
        OpenDataFolderButton.Click += (_, _) =>
            System.Diagnostics.Process.Start("explorer.exe", _settings.ConfigDirectory);
        GitHubButton.Click += (_, _) => OpenUrl(GitHubUrl);
        AboutButton.Click += (_, _) => AboutWindow.ShowDialog(this);
        PromoButton.Click += (_, _) => OpenUrl(PromoUrl);

        // Title-bar minimize behaves like the close button: minimize to tray.
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized && !AllowClose)
            {
                WindowState = WindowState.Normal;
                Hide();
            }
        };

        // Overlay follows window visibility: shown with the settings window, hidden with it.
        IsVisibleChanged += (_, _) => RefreshOverlay();
        _edgeMap.EdgeMapChanged += _ => RefreshOverlay();
        Closed += (_, _) => _visualizer.Hide();

        // Keep the pause checkbox in sync when the tray menu toggles it while open.
        _settings.SettingsChanged += OnExternalSettingsChanged;
        Closed += (_, _) => _settings.SettingsChanged -= OnExternalSettingsChanged;
    }

    // ---------- construction ----------

    private void BuildGesturePanel()
    {
        AddHeader(GesturePanel, Strings.Settings_BasicHeader);
        AddParamRow(GesturePanel, Strings.Settings_TriggerWidth, Strings.Tip_TriggerWidth,
            GestureParameters.MinTriggerWidth, GestureParameters.MaxTriggerWidth,
            s => s.TriggerWidth, (s, v) => s.TriggerWidth = v);
        AddParamRow(GesturePanel, Strings.Settings_Sensitivity, Strings.Tip_Sensitivity,
            GestureParameters.MinSegmentThreshold, GestureParameters.MaxSegmentThreshold,
            s => s.SegmentThreshold, (s, v) => s.SegmentThreshold = v);
        AddParamRow(GesturePanel, Strings.Settings_RubCount, Strings.Tip_RubCount,
            GestureParameters.MinSegmentCount, GestureParameters.MaxSegmentCount,
            s => s.SegmentCount, (s, v) => s.SegmentCount = v);
        AddParamRow(GesturePanel, Strings.Settings_NoiseFloor, Strings.Tip_NoiseFloor,
            GestureParameters.MinReversalNoiseFloorPx, GestureParameters.MaxReversalNoiseFloorPx,
            s => s.ReversalNoiseFloorPx, (s, v) => s.ReversalNoiseFloorPx = v);

        _rubCountWarning = new TextBlock
        {
            Text = Strings.Settings_RubCountOneWarning,
            Foreground = Brushes.DarkOrange,
            TextWrapping = TextWrapping.Wrap,
        };
        GesturePanel.Children.Add(_rubCountWarning);

        AddHeader(GesturePanel, Strings.Settings_AdvancedHeader);
        AddParamRow(GesturePanel, Strings.Settings_HoldZoneWidth, Strings.Tip_HoldZoneWidth,
            GestureParameters.MinHoldZoneWidth, GestureParameters.MaxHoldZoneWidth,
            s => s.HoldZoneWidth, (s, v) => s.HoldZoneWidth = v);
        AddParamRow(GesturePanel, Strings.Settings_HoldZoneGrace, Strings.Tip_HoldZoneGrace,
            GestureParameters.MinHoldZoneGraceMs, GestureParameters.MaxHoldZoneGraceMs,
            s => s.HoldZoneGraceMs, (s, v) => s.HoldZoneGraceMs = v);
        AddParamRow(GesturePanel, Strings.Settings_FirstSegmentTimeout, Strings.Tip_FirstSegmentTimeout,
            GestureParameters.MinFirstSegmentTimeoutMs, GestureParameters.MaxFirstSegmentTimeoutMs,
            s => s.FirstSegmentTimeoutMs, (s, v) => s.FirstSegmentTimeoutMs = v);
        AddParamRow(GesturePanel, Strings.Settings_InterSegmentTimeout, Strings.Tip_InterSegmentTimeout,
            GestureParameters.MinInterSegmentTimeoutMs, GestureParameters.MaxInterSegmentTimeoutMs,
            s => s.InterSegmentTimeoutMs, (s, v) => s.InterSegmentTimeoutMs = v);
        AddParamRow(GesturePanel, Strings.Settings_Cooldown, Strings.Tip_Cooldown,
            GestureParameters.MinCooldownMs, GestureParameters.MaxCooldownMs,
            s => s.CooldownMs, (s, v) => s.CooldownMs = v);
        AddParamRow(GesturePanel, Strings.Settings_JumpThreshold, Strings.Tip_JumpThreshold,
            GestureParameters.MinJumpThresholdPx, GestureParameters.MaxJumpThresholdPx,
            s => s.JumpThresholdPx, (s, v) => s.JumpThresholdPx = v);

        GesturePanel.Children.Add(new TextBlock
        {
            Text = Strings.Settings_HighDpiHint,
            Foreground = (Brush)FindResource("TextDimBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0),
        });
    }

    private void BuildBehaviorPanel()
    {
        AddHeader(BehaviorPanel, Strings.Settings_BehaviorHeader);

        _disableFullscreenCheck = AddCheckBox(Strings.Settings_DisableFullscreen, Strings.Tip_DisableFullscreen,
            (s, v) => s.DisableWhenFullscreen = v);
        _autoStartCheck = new CheckBox { Content = Strings.Settings_AutoStart, ToolTip = Strings.Tip_AutoStart, Margin = new Thickness(0, 8, 0, 0) };
        _autoStartCheck.Click += (_, _) => OnAutoStartToggled();
        BehaviorPanel.Children.Add(_autoStartCheck);
        _pausedCheck = AddCheckBox(Strings.Settings_Paused, Strings.Tip_Paused,
            (s, v) => s.Paused = v);
        _enableLoggingCheck = AddCheckBox(Strings.Settings_EnableLogging, Strings.Tip_EnableLogging,
            (s, v) => s.EnableLogging = v);
        _zoneVisualizationCheck = AddCheckBox(Strings.Settings_ZoneVisualization, Strings.Tip_ZoneVisualization,
            (s, v) => s.ShowZoneVisualization = v);

        var blocklistLabel = new TextBlock
        {
            Text = Strings.Settings_Blocklist,
            ToolTip = Strings.Tip_Blocklist,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 16, 0, 4),
        };
        BehaviorPanel.Children.Add(blocklistLabel);
        _blocklistBox = new ListBox { Height = 110, ToolTip = Strings.Tip_Blocklist };
        BehaviorPanel.Children.Add(_blocklistBox);
        _blocklistInput = new TextBox { Width = 160, ToolTip = Strings.Tip_Blocklist };
        var addButton = new Button { Content = Strings.Settings_BlocklistAdd, Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(8, 0, 0, 0) };
        var removeButton = new Button { Content = Strings.Settings_BlocklistRemove, Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(8, 0, 0, 0) };
        addButton.Click += (_, _) => OnBlocklistAdd();
        removeButton.Click += (_, _) => OnBlocklistRemove();
        var blocklistRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        blocklistRow.Children.Add(_blocklistInput);
        blocklistRow.Children.Add(addButton);
        blocklistRow.Children.Add(removeButton);
        BehaviorPanel.Children.Add(blocklistRow);

        var languageLabel = new TextBlock
        {
            Text = Strings.Settings_Language,
            ToolTip = Strings.Tip_Language,
            Margin = new Thickness(0, 16, 0, 4),
        };
        BehaviorPanel.Children.Add(languageLabel);
        _languageCombo = new ComboBox
        {
            Width = 160,
            HorizontalAlignment = HorizontalAlignment.Left,
            ToolTip = Strings.Tip_Language,
            ItemsSource = new[]
            {
                new LanguageOption(AppSettings.LanguageSystem, Strings.Settings_LanguageSystem),
                new LanguageOption("zh-CN", "中文"),
                new LanguageOption("en", "English"),
            },
            DisplayMemberPath = nameof(LanguageOption.Label),
            SelectedValuePath = nameof(LanguageOption.Id),
        };
        _languageCombo.SelectionChanged += (_, _) => OnLanguageChanged();
        BehaviorPanel.Children.Add(_languageCombo);
        _languageRestartNotice = new TextBlock
        {
            Text = Strings.Settings_LanguageRestartNotice,
            Foreground = Brushes.DarkOrange,
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 4, 0, 0),
        };
        BehaviorPanel.Children.Add(_languageRestartNotice);
    }

    private static void AddHeader(Panel parent, string text) =>
        parent.Children.Add(new TextBlock
        {
            Text = text,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 6),
        });

    private void BuildAttribution()
    {
        // License-required attribution for Assets/screen.png — keep the text verbatim.
        var link = new System.Windows.Documents.Hyperlink
        {
            NavigateUri = new Uri("https://www.flaticon.com/free-icons/screen"),
        };
        link.Inlines.Add("Screen icons created by Magnific - Flaticon");
        link.RequestNavigate += (_, e) =>
        {
            OpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
        };
        var versionLink = new System.Windows.Documents.Hyperlink
        {
            NavigateUri = new Uri(ReleasesUrl),
        };
        versionLink.Inlines.Add(AppVersion.Current);
        versionLink.RequestNavigate += (_, e) =>
        {
            OpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
        };
        AttributionText.Inlines.Add(versionLink);
        AttributionText.Inlines.Add("  |  ");
        AttributionText.Inlines.Add("Icon: ");
        AttributionText.Inlines.Add(link);
    }

    private static void OpenUrl(string url) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
        {
            UseShellExecute = true,
        });

    private CheckBox AddCheckBox(string label, string tooltip, Action<AppSettings, bool> set)
    {
        var check = new CheckBox { Content = label, ToolTip = tooltip, Margin = new Thickness(0, 8, 0, 0) };
        check.Click += (_, _) =>
        {
            if (!_loading)
                _settings.Update(s => set(s, check.IsChecked == true));
        };
        BehaviorPanel.Children.Add(check);
        return check;
    }

    private void AddParamRow(Panel parent, string label, string tooltip, int sliderMin, int sliderMax,
        Func<AppSettings, int> get, Action<AppSettings, int> set)
    {
        var labelBlock = new TextBlock
        {
            Text = label,
            ToolTip = tooltip,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        var slider = new Slider
        {
            Minimum = sliderMin,
            Maximum = sliderMax,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = tooltip,
        };
        var box = new TextBox { Width = 64, ToolTip = tooltip, VerticalContentAlignment = VerticalAlignment.Center };

        var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(slider, 1);
        Grid.SetColumn(box, 2);
        row.Children.Add(labelBlock);
        row.Children.Add(slider);
        row.Children.Add(box);
        parent.Children.Add(row);

        slider.ValueChanged += (_, _) =>
        {
            if (_loading)
                return;
            _settings.Update(s => set(s, (int)slider.Value));
            RefreshParams();
        };
        box.LostFocus += (_, _) => CommitText(box, set);
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                CommitText(box, set);
                e.Handled = true;
            }
        };
        _paramRows.Add(new ParamRow(slider, box, get));
    }

    // ---------- state ----------

    private void LoadState()
    {
        _loading = true;
        try
        {
            var s = _settings.Settings;
            _disableFullscreenCheck!.IsChecked = s.DisableWhenFullscreen;
            _pausedCheck!.IsChecked = s.Paused;
            _enableLoggingCheck!.IsChecked = s.EnableLogging;
            _zoneVisualizationCheck!.IsChecked = s.ShowZoneVisualization;
            // The registry is the source of truth for autostart, not the JSON file.
            _autoStartCheck!.IsChecked = _autostart.IsEnabled();
            _languageCombo!.SelectedValue = s.Language;
            _languageRestartNotice!.Visibility = Visibility.Collapsed;
            RefreshBlocklist();
        }
        finally
        {
            _loading = false;
        }
        RefreshParams();
    }

    private void RefreshParams()
    {
        _loading = true;
        try
        {
            var s = _settings.Settings;
            foreach (var row in _paramRows)
            {
                row.Box.Text = row.Get(s).ToString();
                // The slider clamps itself into its range; the text box shows the real value.
                row.Slider.Value = row.Get(s);
            }
            if (_rubCountWarning != null)
                _rubCountWarning.Visibility = s.SegmentCount <= 1 ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            _loading = false;
        }
    }

    private void CommitText(TextBox box, Action<AppSettings, int> set)
    {
        if (_loading)
            return;
        // Typed input is not limited to the slider range; Sanitize enforces only the
        // hard algorithmic invariants. Invalid text simply reverts on refresh.
        if (int.TryParse(box.Text.Trim(), out var value))
            _settings.Update(s => set(s, value));
        RefreshParams();
    }

    // ---------- behavior handlers ----------

    private void OnAutoStartToggled()
    {
        if (_loading)
            return;
        var enable = _autoStartCheck!.IsChecked == true;
        if (!_autostart.SetEnabled(enable))
        {
            // Denied by policy/AV: snap the toggle back and warn.
            _loading = true;
            _autoStartCheck.IsChecked = !enable;
            _loading = false;
            AppMessageBox.Show(this, Strings.Settings_AutostartDenied, Title);
            return;
        }
        _settings.Update(s => s.AutoStart = enable);
    }

    private void OnBlocklistAdd()
    {
        var name = _blocklistInput!.Text.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = name[..^4]; // normalize to the Process.ProcessName convention
        if (name.Length == 0)
            return;
        _settings.Update(s =>
        {
            if (!s.BlockedProcesses.Contains(name, StringComparer.OrdinalIgnoreCase))
                s.BlockedProcesses.Add(name);
        });
        _blocklistInput.Clear();
        RefreshBlocklist();
    }

    private void OnBlocklistRemove()
    {
        if (_blocklistBox!.SelectedItem is not string selected)
            return;
        _settings.Update(s => s.BlockedProcesses.Remove(selected));
        RefreshBlocklist();
    }

    private void OnLanguageChanged()
    {
        if (_loading || _languageCombo!.SelectedValue is not string language)
            return;
        _settings.Update(s => s.Language = language);
        _languageRestartNotice!.Visibility =
            language != _languageAtOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnResetDefaults()
    {
        if (!AppMessageBox.Confirm(this, Strings.Settings_ResetConfirm, Title))
            return;
        _settings.Update(s => s.CopyFrom(new AppSettings()));
        // Defaults have autostart off; apply that to the registry as well.
        if (_autostart.IsEnabled())
            _autostart.SetEnabled(false);
        LoadState();
    }

    private void OnExternalSettingsChanged()
    {
        if (_loading)
            return;
        _loading = true;
        try
        {
            _pausedCheck!.IsChecked = _settings.Settings.Paused;
        }
        finally
        {
            _loading = false;
        }
        RefreshOverlay(); // parameter changes must redraw the zones immediately
    }

    private void RefreshOverlay()
    {
        if (IsVisible && _settings.Settings.ShowZoneVisualization)
            _visualizer.Show(_edgeMap.Segments, _settings.Settings);
        else
            _visualizer.Hide();
    }

    private void RefreshBlocklist()
    {
        _blocklistBox!.ItemsSource = null;
        _blocklistBox.ItemsSource = _settings.Settings.BlockedProcesses;
    }

    /// <summary>Every close path (buttons, title-bar X) minimizes to tray instead of
    /// closing; only TrayApp's real shutdown sets <see cref="AllowClose"/>.</summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }
}
