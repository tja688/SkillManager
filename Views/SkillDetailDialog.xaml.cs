using SkillManager.Models;
using System.Windows;
using Wpf.Ui.Controls;

namespace SkillManager.Views;

public partial class SkillDetailDialog : FluentWindow
{
    public SkillDetailDialog(SkillFolder skill)
    {
        DataContext = skill;
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
