using SkillManager.Models;
using SkillManager.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SkillManager.Views;

/// <summary>
/// ProjectDetailPage.xaml 的交互逻辑
/// </summary>
public partial class ProjectDetailPage : Page
{
    public ProjectDetailViewModel ViewModel { get; }

    public ProjectDetailPage(ProjectDetailViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void AddSkillZone_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddSkillZoneDialog();
        dialog.Owner = Window.GetWindow(this);

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ZoneName))
        {
            ViewModel.AddSkillZoneCommand.Execute(dialog.ZoneName);
        }
    }

    private void AddSkillToZone_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is SkillZone zone)
        {
            ViewModel.SelectedZone = zone;
            ShowSelectSkillDialog(false);
        }
    }

    private void AddSkillToProject_Click(object sender, RoutedEventArgs e)
    {
        ShowSelectSkillDialog(true);
    }

    private void ShowSelectSkillDialog(bool addToProject)
    {
        ViewModel.LoadLibrarySkills();

        var dialog = new SelectSkillDialog(ViewModel.FilteredLibrarySkills, skill =>
        {
            ViewModel.SearchLibrarySkillsCommand.Execute(skill);
        });
        dialog.Owner = Window.GetWindow(this);

        if (dialog.ShowDialog() == true && dialog.SelectedSkill != null)
        {
            if (addToProject)
            {
                ViewModel.AddSkillToProjectCommand.Execute(dialog.SelectedSkill);
            }
            else
            {
                ViewModel.AddSkillToZoneCommand.Execute(dialog.SelectedSkill);
            }
        }
    }

    private void DeleteSkillZone_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is SkillZone zone)
        {
            var result = System.Windows.MessageBox.Show(
                $"确定要删除技能区 \"{zone.Name}\" 吗？\n\n此操作会删除实际的文件夹，无法恢复！",
                "确认删除",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                ViewModel.DeleteSkillZoneCommand.Execute(zone);
            }
        }
    }

    private void OpenZoneFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is SkillZone zone)
        {
            ViewModel.OpenZoneFolderCommand.Execute(zone);
        }
    }

    private void DeleteSkillFromZone_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is SkillFolder skill)
        {
            var result = System.Windows.MessageBox.Show(
                $"确定要删除技能 \"{skill.Name}\" 吗？\n\n此操作会删除实际的技能文件夹，无法恢复！",
                "确认删除",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                ViewModel.DeleteSkillFromZoneCommand.Execute(skill);
            }
        }
    }

    private void OpenSkillFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is SkillFolder skill)
        {
            ViewModel.OpenSkillFolderCommand.Execute(skill);
        }
    }

    private void ViewSkillMd_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is SkillFolder skill)
        {
            ViewModel.ViewSkillMdCommand.Execute(skill);
        }
    }

    private void OpenProjectFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CurrentProject != null && System.IO.Directory.Exists(ViewModel.CurrentProject.Path))
        {
            System.Diagnostics.Process.Start("explorer.exe", ViewModel.CurrentProject.Path);
        }
    }
}
