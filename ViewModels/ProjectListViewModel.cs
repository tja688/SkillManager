using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkillManager.Models;
using SkillManager.Services;
using System.Collections.ObjectModel;

namespace SkillManager.ViewModels;

/// <summary>
/// 项目列表页面ViewModel
/// </summary>
public partial class ProjectListViewModel : ObservableObject
{
    private readonly ProjectService _projectService;
    private readonly Action<Project> _navigateToDetail;

    public ProjectListViewModel(ProjectService projectService, Action<Project> navigateToDetail)
    {
        _projectService = projectService;
        _navigateToDetail = navigateToDetail;
        Projects = new ObservableCollection<Project>();
    }

    [ObservableProperty]
    private string _statusMessage = "";

    public ObservableCollection<Project> Projects { get; }

    /// <summary>
    /// 刷新项目列表
    /// </summary>
    [RelayCommand]
    public async Task RefreshProjectsAsync()
    {
        Projects.Clear();
        var projects = _projectService.GetAllProjects();

        foreach (var project in projects)
        {
            // 加载技能区以获取技能数
            await _projectService.LoadSkillZonesAsync(project);
            Projects.Add(project);
        }

        StatusMessage = $"共 {Projects.Count} 个项目";
    }

    /// <summary>
    /// 打开项目详情
    /// </summary>
    [RelayCommand]
    private void OpenProject(Project? project)
    {
        if (project != null)
        {
            _navigateToDetail(project);
        }
    }

    /// <summary>
    /// 删除项目（假删）
    /// </summary>
    [RelayCommand]
    private async Task DeleteProjectAsync(Project? project)
    {
        if (project == null) return;

        await _projectService.DeleteProjectAsync(project);
        Projects.Remove(project);
        StatusMessage = $"已删除项目: {project.Name}";
    }

    /// <summary>
    /// 打开项目文件夹
    /// </summary>
    [RelayCommand]
    private void OpenProjectFolder(Project? project)
    {
        if (project != null && System.IO.Directory.Exists(project.Path))
        {
            System.Diagnostics.Process.Start("explorer.exe", project.Path);
        }
    }

    /// <summary>
    /// 获取ProjectService
    /// </summary>
    public ProjectService ProjectService => _projectService;
}
