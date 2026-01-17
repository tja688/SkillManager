using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkillManager.Models;
using SkillManager.Services;
using System.Collections.ObjectModel;
using System.IO;

namespace SkillManager.ViewModels;

/// <summary>
/// Library页面ViewModel
/// </summary>
public partial class LibraryViewModel : ObservableObject
{
    private readonly LibraryService _libraryService;

    public LibraryViewModel(LibraryService libraryService)
    {
        _libraryService = libraryService;
        Skills = new ObservableCollection<SkillFolder>();
    }

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private SkillFolder? _selectedSkill;

    public ObservableCollection<SkillFolder> Skills { get; }

    /// <summary>
    /// 刷新技能列表
    /// </summary>
    [RelayCommand]
    public void RefreshSkills()
    {
        Skills.Clear();
        var skills = _libraryService.GetAllSkills();

        foreach (var skill in skills)
        {
            // 如果有搜索文本，过滤
            if (!string.IsNullOrEmpty(SearchText) &&
                !skill.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                !skill.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Skills.Add(skill);
        }

        StatusMessage = $"共 {Skills.Count} 个技能";
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshSkills();
    }

    [RelayCommand]
    private async Task DeleteSkillAsync(SkillFolder? skill)
    {
        if (skill == null) return;

        var progress = new Progress<string>(msg => StatusMessage = msg);
        var success = await _libraryService.DeleteSkillAsync(skill, progress);

        if (success)
        {
            Skills.Remove(skill);
            StatusMessage = $"已删除: {skill.Name}";
        }
    }

    [RelayCommand]
    private void OpenFolder(SkillFolder? skill)
    {
        if (skill != null)
        {
            _libraryService.OpenSkillFolder(skill);
        }
    }

    [RelayCommand]
    private void OpenLibraryFolder()
    {
        System.Diagnostics.Process.Start("explorer.exe", _libraryService.LibraryPath);
    }

    /// <summary>
    /// 查看SKILL.md内容
    /// </summary>
    [RelayCommand]
    private void ViewSkillMd(SkillFolder? skill)
    {
        if (skill == null) return;

        var skillMdPath = skill.SkillMdPath;
        if (File.Exists(skillMdPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = skillMdPath,
                UseShellExecute = true
            });
        }
    }
}
