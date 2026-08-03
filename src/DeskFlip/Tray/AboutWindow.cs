using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using DeskFlip.Properties;

namespace DeskFlip.Tray;

/// <summary>
/// About dialog (dark-themed, like <see cref="AppMessageBox"/>): version, author,
/// and the verbatim limitation-of-liability text (legal text — English in all languages).
/// </summary>
public static class AboutWindow
{
    private const string AuthorUrl = "https://github.com/AaronFeng753";

    private const string Disclaimer =
        "Limitation of Liability\n" +
        "THIS SOFTWARE IS PROVIDED \"AS IS\" WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESSED OR IMPLIED, " +
        "INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A " +
        "PARTICULAR PURPOSE. THE ENTIRE RISK AS TO THE QUALITY AND PERFORMANCE OF THE SOFTWARE IS WITH " +
        "THE LICENSEE. SHOULD THE SOFTWARE PROVE DEFECTIVE, YOU ASSUME THE COST OF ALL NECESSARY " +
        "SERVICING, REPAIR OR CORRECTION. IF ANY ALGORITHM IS PATENTED IN YOUR COUNTRY, YOU SHOULD " +
        "ACQUIRE A LICENSE BEFORE USING THIS COMPONENT.\n\n" +
        "IN NO EVENT WILL THE AUTHOR OR ANY OTHER PARTY WHO MAY HAVE DISTRIBUTED THE SOFTWARE AS " +
        "PERMITTED ABOVE, BE LIABLE TO YOU FOR DAMAGES, INCLUDING ANY GENERAL, SPECIAL, INCIDENTAL OR " +
        "CONSEQUENTIAL DAMAGES ARISING OUT OF THE USE OR INABILITY TO USE THE SOFTWARE (INCLUDING BUT " +
        "NOT LIMITED TO LOSS OF DATA OR DATA BEING RENDERED INACCURATE OR LOSSES SUSTAINED BY YOU OR " +
        "THIRD PARTIES OR A FAILURE OF THE SOFTWARE TO OPERATE WITH ANY OTHER PROGRAMS), EVEN IF SUCH " +
        "HOLDER OR OTHER PARTY HAS BEEN ADVISED OF THE POSSIBILITY OF SUCH DAMAGES.\n\n" +
        "YOU ACKNOWLEDGE THAT YOU HAVE READ THIS AGREEMENT, UNDERSTAND IT AND AGREE TO BE BOUND BY " +
        "ITS TERMS AND CONDITIONS.";

    public static void ShowDialog(Window owner)
    {
        var dialog = new Window
        {
            Title = Strings.About_Title,
            Width = 480,
            SizeToContent = SizeToContent.Height,
            MaxHeight = 640,
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
            Text = $"DeskFlip — {AppVersion.Current}",
            FontWeight = FontWeights.SemiBold,
        });

        var author = new TextBlock { Margin = new Thickness(0, 12, 0, 0) };
        author.Inlines.Add("Aaron Feng — ");
        var link = new Hyperlink { NavigateUri = new Uri(AuthorUrl) };
        link.Inlines.Add(AuthorUrl);
        link.RequestNavigate += (_, e) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true,
            });
            e.Handled = true;
        };
        author.Inlines.Add(link);
        layout.Children.Add(author);

        var license = new TextBlock { Margin = new Thickness(0, 6, 0, 0) };
        license.Inlines.Add($"{Strings.About_License} ");
        license.Inlines.Add("GNU Affero General Public License v3.0");
        layout.Children.Add(license);

        layout.Children.Add(new TextBlock
        {
            Text = Strings.About_Disclaimer,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 16, 0, 6),
        });
        layout.Children.Add(new ScrollViewer
        {
            MaxHeight = 380,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new TextBlock
            {
                Text = Disclaimer,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextDimBrush"],
                FontSize = 11,
            },
        });

        var okButton = new Button
        {
            Content = Strings.Common_OK,
            MinWidth = 84,
            IsDefault = true,
            IsCancel = true,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0),
        };
        okButton.Click += (_, _) => dialog.Close();
        layout.Children.Add(okButton);

        dialog.Content = layout;
        dialog.ShowDialog();
    }
}
