using System.Windows;
using Wpf.Ui.Controls;

namespace SkillManager.Views;

/// <summary>
/// AddSkillZoneDialog.xaml 的交互逻辑
/// </summary>
public partial class AddSkillZoneDialog : FluentWindow
{
    public string ZoneName { get; private set; } = string.Empty;

    public AddSkillZoneDialog()
    {
        InitializeComponent();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        var name = ZoneNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        // 确保以"."开头
        if (!name.StartsWith("."))
        {
            name = "." + name;
        }

        ZoneName = name;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
