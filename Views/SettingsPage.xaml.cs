using System.Windows.Controls;
using Wpf.Ui.Appearance;

namespace SkillManager.Views;

/// <summary>
/// SettingsPage.xaml 的交互逻辑
/// </summary>
public partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedIndex = ThemeComboBox.SelectedIndex;

        var theme = selectedIndex switch
        {
            1 => ApplicationTheme.Light,
            2 => ApplicationTheme.Dark,
            _ => ApplicationTheme.Unknown
        };

        if (theme == ApplicationTheme.Unknown)
        {
            ApplicationThemeManager.ApplySystemTheme();
        }
        else
        {
            ApplicationThemeManager.Apply(theme);
        }
    }
}
