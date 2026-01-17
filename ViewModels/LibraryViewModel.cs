using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkillManager.Models;
using SkillManager.Services;
using SkillManager.Views;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace SkillManager.ViewModels;

/// <summary>
/// 技能库视图模型
/// </summary>
public partial class LibraryViewModel : ObservableObject
{
    private readonly LibraryService _libraryService;
    private readonly GroupService _groupService;
    private readonly List<SkillFolder> _allSkills = new();
    private string? _pendingGroupId;

    public LibraryViewModel(LibraryService libraryService, GroupService groupService)
    {
        _libraryService = libraryService;
        _groupService = groupService;
        
        Skills = new ObservableCollection<SkillFolder>();
        Groups = new ObservableCollection<SkillGroup>();
        FilteredSkills = new ObservableCollection<SkillFolder>();
    }

    public event Action? GroupsRefreshed;

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

    [ObservableProperty]
    private bool _isMultiSelectMode;

    [ObservableProperty]
    private int _selectedSkillCount;

    [ObservableProperty]
    private bool _hasSelection;

    partial void OnSelectedGroupChanged(SkillGroup? value)
    {
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    public void SelectGroupById(string? groupId)
    {
        if (Groups.Count == 0)
        {
            _pendingGroupId = groupId;
            return;
        }

        var resolvedId = groupId ?? string.Empty;
        var match = Groups.FirstOrDefault(g => g.Id == resolvedId)
                    ?? Groups.FirstOrDefault(g => g.Name.Equals(resolvedId, StringComparison.OrdinalIgnoreCase))
                    ?? Groups.FirstOrDefault();
        if (match != null)
        {
            if (!ReferenceEquals(SelectedGroup, match))
            {
                SelectedGroup = match;
            }
            else
            {
                ApplyFilter();
            }
        }

        _pendingGroupId = null;
    }

    [RelayCommand]
    public async Task RefreshSkills()
    {
        try
        {
            IsLoading = true;
            
            var groups = await _groupService.GetAllGroupsAsync();
            Groups.Clear();
            Groups.Add(new SkillGroup { Id = string.Empty, Name = "所有技能" });
            foreach (var group in groups)
            {
                Groups.Add(group);
            }

            var currentGroupId = _pendingGroupId ?? SelectedGroup?.Id ?? string.Empty;
            SelectGroupById(currentGroupId);

            _allSkills.Clear();
            _allSkills.AddRange(await _libraryService.GetAllSkillsAsync(true));
            ApplyGroupDisplay(_allSkills, groups);

            ApplyFilter();
            GroupsRefreshed?.Invoke();
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

    private void ApplyGroupDisplay(IEnumerable<SkillFolder> skills, IEnumerable<SkillGroup> groups)
    {
        var groupLookup = groups
            .SelectMany(group => group.SkillNames.Select(name => new { name, group.Name }))
            .GroupBy(item => item.name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name).ToList(),
                StringComparer.OrdinalIgnoreCase);

        foreach (var skill in skills)
        {
            if (groupLookup.TryGetValue(skill.Name, out var groupNames) && groupNames.Count > 0)
            {
                skill.GroupNamesDisplay = string.Join(", ", groupNames);
            }
            else
            {
                skill.GroupNamesDisplay = "未分组";
            }
        }
    }

    private void ApplyFilter()
    {
        if (_allSkills == null) return;

        if (IsMultiSelectMode || SelectedSkillCount > 0)
        {
            ClearSelection();
        }

        var filtered = _allSkills.AsEnumerable();

        if (SelectedGroup != null && !string.IsNullOrEmpty(SelectedGroup.Id))
        {
            var skillNameSet = new HashSet<string>(
                SelectedGroup.SkillNames ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(s => skillNameSet.Contains(s.Name));
        }

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
        
        var result = MessageBox.Show(
            $"确定要删除技能 '{skill.Name}' 吗？\n此操作将永久删除文件且无法撤销。",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            if (await _libraryService.DeleteSkillAsync(skill))
            {
                await _groupService.RemoveSkillFromAllGroupsAsync(skill.Name);
                await RefreshSkills();
            }
        }
    }

    [RelayCommand]
    public async Task ManageGroups()
    {
        var dialog = new ManageGroupsDialog(_groupService)
        {
            Owner = Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            await RefreshSkills();
        }
    }

    [RelayCommand]
    public async Task ManageSkillGroups(SkillFolder skill)
    {
        if (skill == null) return;
        
        var dialog = new ManageSkillGroupsDialog(_groupService, skill.Name)
        {
            Owner = Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            await RefreshSkills();
        }
    }

    [RelayCommand]
    public void EnterMultiSelectMode(SkillFolder skill)
    {
        if (skill == null) return;

        if (!IsMultiSelectMode)
        {
            IsMultiSelectMode = true;
        }

        ToggleSelectionInternal(skill);
    }

    [RelayCommand]
    public void SelectAllFiltered()
    {
        if (FilteredSkills.Count == 0) return;

        IsMultiSelectMode = true;
        foreach (var skill in FilteredSkills)
        {
            skill.IsSelected = true;
        }
        UpdateSelectionState();
    }

    [RelayCommand]
    public async Task AddSelectedToGroups()
    {
        var selectedSkills = _allSkills.Where(skill => skill.IsSelected).ToList();
        if (selectedSkills.Count == 0) return;

        var dialog = new AddSkillsToGroupsDialog(_groupService, selectedSkills.Count)
        {
            Owner = Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            var groupIds = dialog.SelectedGroupIds;
            await _groupService.AddSkillsToGroupsAsync(selectedSkills.Select(skill => skill.Name), groupIds);
            await RefreshSkills();
            ClearSelection();
        }
    }

    public void HandleCardClick(SkillFolder skill)
    {
        if (skill == null) return;

        if (IsMultiSelectMode)
        {
            ToggleSelectionInternal(skill);
            return;
        }

        var dialog = new SkillDetailDialog(skill)
        {
            Owner = Application.Current?.MainWindow
        };
        dialog.ShowDialog();
    }

    private void ToggleSelectionInternal(SkillFolder skill)
    {
        skill.IsSelected = !skill.IsSelected;
        UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        SelectedSkillCount = _allSkills.Count(skill => skill.IsSelected);
        HasSelection = SelectedSkillCount > 0;

        if (IsMultiSelectMode && SelectedSkillCount == 0)
        {
            IsMultiSelectMode = false;
        }
    }

    private void ClearSelection()
    {
        foreach (var skill in _allSkills)
        {
            skill.IsSelected = false;
        }

        SelectedSkillCount = 0;
        HasSelection = false;
        IsMultiSelectMode = false;
    }
}
