using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkillManager.Models;
using SkillManager.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using Wpf.Ui.Controls;

namespace SkillManager.ViewModels;

/// <summary>
/// 技能库视图模型
/// </summary>
public partial class LibraryViewModel : ObservableObject
{
    private readonly LibraryService _libraryService;
    private readonly GroupService _groupService;
    private List<SkillFolder> _allSkills = new();

    public LibraryViewModel(LibraryService libraryService, GroupService groupService)
    {
        _libraryService = libraryService;
        _groupService = groupService;
        
        Skills = new ObservableCollection<SkillFolder>();
        Groups = new ObservableCollection<SkillGroup>();
        FilteredSkills = new ObservableCollection<SkillFolder>();
    }

    [ObservableProperty]
    private ObservableCollection<SkillFolder> _skills;
    
    [ObservableProperty]
    private ObservableCollection<SkillFolder> _filteredSkills;

    [ObservableProperty]
    private ObservableCollection<SkillGroup> _groups;

    [ObservableProperty]
    private SkillGroup? _selectedGroup;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSelectedGroupChanged(SkillGroup? value)
    {
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    public async Task RefreshSkills()
    {
        try
        {
            IsLoading = true;
            
            // 加载分组
            var groups = await _groupService.GetAllGroupsAsync();
            Groups.Clear();
            // 添加"所有"选项
            Groups.Add(new SkillGroup { Id = string.Empty, Name = "所有技能" });
            foreach (var group in groups)
            {
                Groups.Add(group);
            }

            // 保持当前选中的分组
            if (SelectedGroup != null)
            {
                SelectedGroup = Groups.FirstOrDefault(g => g.Id == SelectedGroup.Id) ?? Groups.First();
            }
            else
            {
                SelectedGroup = Groups.First();
            }

            // 加载技能
            _allSkills = await _libraryService.GetAllSkillsAsync(true);
            
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading skills: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        if (_allSkills == null) return;

        var filtered = _allSkills.AsEnumerable();

        // 分组筛选
        if (SelectedGroup != null && !string.IsNullOrEmpty(SelectedGroup.Id))
        {
            filtered = filtered.Where(s => SelectedGroup.SkillNames.Contains(s.Name));
        }

        // 搜索筛选
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(s => 
                s.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || 
                (s.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.SkillTitle?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        FilteredSkills.Clear();
        foreach (var skill in filtered)
        {
            FilteredSkills.Add(skill);
        }
    }

    [RelayCommand]
    public void OpenSkillFolder(SkillFolder skill)
    {
        if (skill == null) return;
        _libraryService.OpenSkillFolder(skill);
    }

    [RelayCommand]
    public async Task DeleteSkill(SkillFolder skill)
    {
        if (skill == null) return;
        
        var result = System.Windows.MessageBox.Show(
            $"确定要删除技能 '{skill.Name}' 吗？\n此操作将永久删除文件且无法撤销。",
            "确认删除",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            if (await _libraryService.DeleteSkillAsync(skill))
            {
                // 同时更新分组数据
                await _groupService.RemoveSkillFromAllGroupsAsync(skill.Name);
                await RefreshSkills();
            }
        }
    }

     // 占位功能：管理分组
    [RelayCommand]
    public async Task ManageGroups()
    {
        // 这里应该显示管理分组的对话框
        // 由于View代码丢失，这里先简化处理，实际应该调用ManageGroupsDialog
        // TODO: Restore ManageGroupsDialog interaction
    }

    // 占位功能：管理技能分组
    [RelayCommand]
    public async Task ManageSkillGroups(SkillFolder skill)
    {
        if (skill == null) return;
        
        // 这里应该显示管理技能分组的对话框
        // TODO: Restore ManageSkillGroupsDialog interaction
    }
    
    [RelayCommand]
    public void ToggleExpand(SkillFolder skill)
    {
        if (skill == null) return;
        skill.IsExpanded = !skill.IsExpanded;
    }
}
