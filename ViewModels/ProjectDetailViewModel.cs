using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkillManager.Models;
using SkillManager.Services;
using System.Collections.ObjectModel;

namespace SkillManager.ViewModels;

/// <summary>
/// 项目详情页面ViewModel
/// </summary>
public partial class ProjectDetailViewModel : ObservableObject
{
    private readonly ProjectService _projectService;
    private readonly Action _navigateBack;

    public ProjectDetailViewModel(ProjectService projectService, Action navigateBack)
    {
        _projectService = projectService;
        _navigateBack = navigateBack;
    }

    [ObservableProperty]
    private Project? _currentProject;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private SkillZone? _selectedZone;

    [ObservableProperty]
    private SkillFolder? _selectedSkill;

    /// <summary>
    /// 仓库中的技能列表（用于添加技能时选择）
    /// </summary>
    public ObservableCollection<SkillFolder> LibrarySkills { get; } = new();

    /// <summary>
    /// 过滤后的仓库技能
    /// </summary>
    public ObservableCollection<SkillFolder> FilteredLibrarySkills { get; } = new();

    /// <summary>
    /// 加载项目
    /// </summary>
    public async Task LoadProjectAsync(Project project)
    {
        CurrentProject = project;
        await _projectService.LoadSkillZonesAsync(project);
        await _projectService.LoadExpandStatesAsync(project);
        StatusMessage = $"{project.Name} - 共 {project.TotalSkillCount} 个技能";
    }

    /// <summary>
    /// 刷新项目
    /// </summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (CurrentProject != null)
        {
            await LoadProjectAsync(CurrentProject);
        }
    }

    /// <summary>
    /// 返回项目列表
    /// </summary>
    [RelayCommand]
    private async Task GoBackAsync()
    {
        // 保存展开状态
        if (CurrentProject != null)
        {
            await _projectService.SaveExpandStatesAsync(CurrentProject);
        }
        _navigateBack();
    }

    /// <summary>
    /// 添加技能区
    /// </summary>
    [RelayCommand]
    private async Task AddSkillZoneAsync(string? zoneName)
    {
        if (CurrentProject == null || string.IsNullOrWhiteSpace(zoneName)) return;

        var zone = await _projectService.AddSkillZoneAsync(CurrentProject, zoneName);
        if (zone != null)
        {
            StatusMessage = $"已创建技能区: {zone.Name}";
        }
        else
        {
            StatusMessage = "创建技能区失败";
        }
    }

    /// <summary>
    /// 删除技能区（真删）
    /// </summary>
    [RelayCommand]
    private async Task DeleteSkillZoneAsync(SkillZone? zone)
    {
        if (CurrentProject == null || zone == null) return;

        var success = await _projectService.DeleteSkillZoneAsync(CurrentProject, zone);
        StatusMessage = success ? $"已删除技能区: {zone.Name}" : "删除技能区失败";
    }

    /// <summary>
    /// 打开技能区文件夹
    /// </summary>
    [RelayCommand]
    private void OpenZoneFolder(SkillZone? zone)
    {
        if (zone != null && System.IO.Directory.Exists(zone.FullPath))
        {
            System.Diagnostics.Process.Start("explorer.exe", zone.FullPath);
        }
    }

    /// <summary>
    /// 切换技能区展开状态
    /// </summary>
    [RelayCommand]
    private async Task ToggleZoneExpandedAsync(SkillZone? zone)
    {
        if (zone == null || CurrentProject == null) return;
        zone.IsExpanded = !zone.IsExpanded;
        await _projectService.SaveExpandStatesAsync(CurrentProject);
    }

    /// <summary>
    /// 加载仓库技能列表
    /// </summary>
    public void LoadLibrarySkills()
    {
        LibrarySkills.Clear();
        FilteredLibrarySkills.Clear();

        var skills = _projectService.LibraryService.GetAllSkills();
        foreach (var skill in skills)
        {
            LibrarySkills.Add(skill);
            FilteredLibrarySkills.Add(skill);
        }
    }

    /// <summary>
    /// 搜索仓库技能
    /// </summary>
    [RelayCommand]
    private void SearchLibrarySkills(string? searchText)
    {
        FilteredLibrarySkills.Clear();

        foreach (var skill in LibrarySkills)
        {
            if (string.IsNullOrEmpty(searchText) ||
                skill.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                skill.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                FilteredLibrarySkills.Add(skill);
            }
        }
    }

    /// <summary>
    /// 添加技能到技能区
    /// </summary>
    [RelayCommand]
    private async Task AddSkillToZoneAsync(SkillFolder? skill)
    {
        if (SelectedZone == null || skill == null) return;

        var success = await _projectService.AddSkillToZoneAsync(SelectedZone, skill);
        if (success)
        {
            CurrentProject?.RefreshSkillCount();
            StatusMessage = $"已添加技能 {skill.Name} 到 {SelectedZone.Name}";
        }
        else
        {
            StatusMessage = $"添加技能失败，可能已存在";
        }
    }

    /// <summary>
    /// 添加技能到项目的所有技能区
    /// </summary>
    [RelayCommand]
    private async Task AddSkillToProjectAsync(SkillFolder? skill)
    {
        if (CurrentProject == null || skill == null) return;

        var count = await _projectService.AddSkillToProjectAsync(CurrentProject, skill);
        StatusMessage = count > 0 
            ? $"已将技能 {skill.Name} 添加到 {count} 个技能区" 
            : "添加技能失败";
    }

    /// <summary>
    /// 从技能区删除技能
    /// </summary>
    [RelayCommand]
    private async Task DeleteSkillFromZoneAsync(SkillFolder? skill)
    {
        if (skill == null) return;

        // 找到技能所在的技能区
        var zone = CurrentProject?.SkillZones.FirstOrDefault(z => z.Skills.Contains(skill));
        if (zone == null) return;

        var success = await _projectService.DeleteSkillFromZoneAsync(zone, skill);
        if (success)
        {
            CurrentProject?.RefreshSkillCount();
            StatusMessage = $"已从 {zone.Name} 删除技能: {skill.Name}";
        }
        else
        {
            StatusMessage = "删除技能失败";
        }
    }

    /// <summary>
    /// 从项目的所有技能区删除同名技能
    /// </summary>
    [RelayCommand]
    private async Task DeleteSkillFromProjectAsync(string? skillName)
    {
        if (CurrentProject == null || string.IsNullOrEmpty(skillName)) return;

        var count = await _projectService.DeleteSkillFromProjectAsync(CurrentProject, skillName);
        StatusMessage = count > 0 
            ? $"已从 {count} 个技能区删除技能: {skillName}" 
            : "没有找到该技能";
    }

    /// <summary>
    /// 打开技能文件夹
    /// </summary>
    [RelayCommand]
    private void OpenSkillFolder(SkillFolder? skill)
    {
        if (skill != null && System.IO.Directory.Exists(skill.FullPath))
        {
            System.Diagnostics.Process.Start("explorer.exe", skill.FullPath);
        }
    }

    /// <summary>
    /// 查看技能的SKILL.md
    /// </summary>
    [RelayCommand]
    private void ViewSkillMd(SkillFolder? skill)
    {
        if (skill == null) return;

        var skillMdPath = skill.SkillMdPath;
        if (System.IO.File.Exists(skillMdPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = skillMdPath,
                UseShellExecute = true
            });
        }
    }

    /// <summary>
    /// 获取ProjectService
    /// </summary>
    public ProjectService ProjectService => _projectService;
}
