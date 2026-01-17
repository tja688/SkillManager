using CommunityToolkit.Mvvm.ComponentModel;
using SkillManager.Models;
using SkillManager.Services;
using System.Collections.ObjectModel;
using System.Windows;
using Wpf.Ui.Controls;

namespace SkillManager.Views;

/// <summary>
/// 管理技能分组对话框的ViewModel
/// </summary>
public partial class ManageSkillGroupsDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _skillName = string.Empty;

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
/// 管理技能分组对话框
/// </summary>
public partial class ManageSkillGroupsDialog : FluentWindow
{
    private readonly GroupService _groupService;
    private readonly ManageSkillGroupsDialogViewModel _viewModel;
    private readonly string _skillName;
    private readonly HashSet<string> _originalSelectedGroups;

    public ManageSkillGroupsDialog(GroupService groupService, string skillName)
    {
        _groupService = groupService;
        _skillName = skillName;
        _originalSelectedGroups = new HashSet<string>();
        _viewModel = new ManageSkillGroupsDialogViewModel { SkillName = skillName };
        DataContext = _viewModel;

        InitializeComponent();
        Loaded += async (_, _) => await LoadGroupsAsync();
    }

    private async Task LoadGroupsAsync()
    {
        // 获取所有分组
        var allGroups = await _groupService.GetAllGroupsAsync();
        
        // 获取技能当前所属的分组
        var skillGroups = await _groupService.GetGroupsForSkillAsync(_skillName);
        var skillGroupIds = new HashSet<string>(skillGroups.Select(g => g.Id));

        _viewModel.Groups.Clear();
        foreach (var group in allGroups)
        {
            group.IsSelected = skillGroupIds.Contains(group.Id);
            if (group.IsSelected)
            {
                _originalSelectedGroups.Add(group.Id);
            }
            _viewModel.Groups.Add(group);
        }
        _viewModel.UpdateHasNoGroups();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // 计算需要添加和移除的分组
        var currentSelected = new HashSet<string>(_viewModel.Groups.Where(g => g.IsSelected).Select(g => g.Id));

        // 需要添加的分组
        var toAdd = currentSelected.Except(_originalSelectedGroups);
        // 需要移除的分组
        var toRemove = _originalSelectedGroups.Except(currentSelected);

        foreach (var groupId in toAdd)
        {
            await _groupService.AddSkillToGroupAsync(_skillName, groupId);
        }

        foreach (var groupId in toRemove)
        {
            await _groupService.RemoveSkillFromGroupAsync(_skillName, groupId);
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
