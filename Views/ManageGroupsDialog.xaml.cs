using CommunityToolkit.Mvvm.ComponentModel;
using SkillManager.Models;
using SkillManager.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace SkillManager.Views;

/// <summary>
/// 管理分组对话框的ViewModel
/// </summary>
public partial class ManageGroupsDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<SkillGroup> _groups = new();

    [ObservableProperty]
    private bool _hasNoGroups = true;

    partial void OnGroupsChanged(ObservableCollection<SkillGroup> value)
    {
        HasNoGroups = value.Count == 0;
    }

    public void UpdateHasNoGroups()
    {
        HasNoGroups = Groups.Count == 0;
    }
}

/// <summary>
/// 管理分组对话框
/// </summary>
public partial class ManageGroupsDialog : FluentWindow
{
    private readonly GroupService _groupService;
    private readonly ManageGroupsDialogViewModel _viewModel;

    public ManageGroupsDialog(GroupService groupService)
    {
        _groupService = groupService;
        _viewModel = new ManageGroupsDialogViewModel();
        DataContext = _viewModel;

        InitializeComponent();
        Loaded += async (_, _) => await LoadGroupsAsync();
    }

    private async Task LoadGroupsAsync()
    {
        var groups = await _groupService.GetAllGroupsAsync();
        _viewModel.Groups.Clear();
        foreach (var group in groups)
        {
            _viewModel.Groups.Add(group);
        }
        _viewModel.UpdateHasNoGroups();
    }

    private async void CreateGroupButton_Click(object sender, RoutedEventArgs e)
    {
        await CreateGroupAsync();
    }

    private async void NewGroupNameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await CreateGroupAsync();
        }
    }

    private async Task CreateGroupAsync()
    {
        var name = NewGroupNameTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        try
        {
            var newGroup = await _groupService.CreateGroupAsync(name);
            _viewModel.Groups.Add(newGroup);
            _viewModel.UpdateHasNoGroups();
            NewGroupNameTextBox.Text = string.Empty;
        }
        catch (InvalidOperationException ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "创建失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    private async void DeleteGroupButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is string groupId)
        {
            var group = _viewModel.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group == null) return;

            var result = System.Windows.MessageBox.Show(
                $"确定要删除分组 \"{group.Name}\" 吗？\n\n分组中的技能不会被删除，只会移除分组关联。",
                "确认删除",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                if (await _groupService.DeleteGroupAsync(groupId))
                {
                    _viewModel.Groups.Remove(group);
                    _viewModel.UpdateHasNoGroups();
                }
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
