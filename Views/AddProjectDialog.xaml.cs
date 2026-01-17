using SkillManager.Models;
using SkillManager.Services;
using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace SkillManager.Views;

/// <summary>
/// AddProjectDialog.xaml 的交互逻辑
/// </summary>
public partial class AddProjectDialog : FluentWindow
{
    private readonly ProjectService _projectService;
    private readonly ObservableCollection<SkillZonePreview> _skillZonePreviews = new();

    public Project? CreatedProject { get; private set; }

    public AddProjectDialog(ProjectService projectService)
    {
        _projectService = projectService;
        InitializeComponent();
        SkillZonesPreviewList.ItemsSource = _skillZonePreviews;

        // 监听文本变化
        ProjectNameTextBox.TextChanged += (s, e) => UpdateConfirmButtonState();
        ProjectPathTextBox.TextChanged += (s, e) => UpdateConfirmButtonState();
    }

    private void BrowsePath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择项目路径",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ProjectPathTextBox.Text = dialog.SelectedPath;

            // 自动设置项目名称（如果为空）
            if (string.IsNullOrWhiteSpace(ProjectNameTextBox.Text))
            {
                ProjectNameTextBox.Text = System.IO.Path.GetFileName(dialog.SelectedPath);
            }

            // 扫描技能区
            ScanSkillZonesAsync(dialog.SelectedPath);
        }
    }

    private async void ScanSkillZonesAsync(string path)
    {
        _skillZonePreviews.Clear();
        EmptyPreviewText.Visibility = Visibility.Collapsed;
        LoadingIndicator.Visibility = Visibility.Visible;

        try
        {
            var previews = await _projectService.ScanSkillZonesPreviewAsync(path);

            foreach (var preview in previews)
            {
                _skillZonePreviews.Add(preview);
            }

            if (_skillZonePreviews.Count == 0)
            {
                EmptyPreviewText.Text = "未检测到技能区（可以接受，创建项目后可手动添加）";
                EmptyPreviewText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            EmptyPreviewText.Text = $"扫描出错: {ex.Message}";
            EmptyPreviewText.Visibility = Visibility.Visible;
        }
        finally
        {
            LoadingIndicator.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateConfirmButtonState()
    {
        ConfirmButton.IsEnabled = !string.IsNullOrWhiteSpace(ProjectNameTextBox.Text) &&
                                   !string.IsNullOrWhiteSpace(ProjectPathTextBox.Text);
    }

    private async void Confirm_Click(object sender, RoutedEventArgs e)
    {
        var name = ProjectNameTextBox.Text.Trim();
        var path = ProjectPathTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            ConfirmButton.IsEnabled = false;
            CreatedProject = await _projectService.CreateProjectAsync(name, path);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"创建项目失败: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            ConfirmButton.IsEnabled = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
