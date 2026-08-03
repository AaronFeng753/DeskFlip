using System.Windows;
using System.Windows.Controls;
using DeskFlip.Properties;

namespace DeskFlip.Tray;

/// <summary>
/// Dark-themed replacement for <see cref="MessageBox"/>: the native message
/// box follows the OS theme, not the app theme, and would be the one remaining white
/// flashbang in a dark-only UI.
/// </summary>
public static class AppMessageBox
{
    /// <summary>Information/warning dialog with a single OK button.</summary>
    public static void Show(Window owner, string text, string title) =>
        ShowDialog(owner, text, title, yesNo: false);

    /// <summary>Yes/No confirmation; returns true on Yes.</summary>
    public static bool Confirm(Window owner, string text, string title) =>
        ShowDialog(owner, text, title, yesNo: true);

    private static bool ShowDialog(Window owner, string text, string title, bool yesNo)
    {
        var answer = false;
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false, // tray app: dialogs must not spawn taskbar entries
            Owner = owner,
            Background = (System.Windows.Media.Brush)Application.Current.Resources["BackBrush"],
            Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextBrush"],
        };
        DarkTitleBar.Apply(dialog);

        var layout = new StackPanel { Margin = new Thickness(20) };
        layout.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 18),
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var okButton = new Button { MinWidth = 84, IsDefault = true, IsCancel = !yesNo };
        if (yesNo)
        {
            okButton.Content = Strings.Common_Yes;
            var noButton = new Button { Content = Strings.Common_No, MinWidth = 84, Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
            noButton.Click += (_, _) => dialog.Close();
            buttons.Children.Add(noButton);
        }
        else
        {
            okButton.Content = Strings.Common_OK;
        }
        okButton.Click += (_, _) =>
        {
            answer = true;
            dialog.Close();
        };
        buttons.Children.Insert(0, okButton); // Yes/OK first, No second
        layout.Children.Add(buttons);
        dialog.Content = layout;

        dialog.ShowDialog();
        return answer;
    }
}
