using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkillManager.Models;
using SkillManager.Services;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using System.IO;

namespace SkillManager.ViewModels;

/// <summary>
/// 扫描页面ViewModel
/// </summary>
public partial class ScanViewModel : ObservableObject
{
    private readonly SkillScannerService _scannerService;
    private readonly LibraryService _libraryService;
    private CancellationTokenSource? _cts;

    public ScanViewModel(SkillScannerService scannerService, LibraryService libraryService)
    {
        _scannerService = scannerService;
        _libraryService = libraryService;
        FoundSkills = new ObservableCollection<SkillFolder>();
    }

    [ObservableProperty]
    private string _scanPath = "";

    [ObservableProperty]
    private string _statusMessage = "选择路径开始扫描";

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private string _scanStats = "";

    public ObservableCollection<SkillFolder> FoundSkills { get; }

    [RelayCommand]
    private void BrowsePath()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择扫描目录"
        };

        if (dialog.ShowDialog() == true)
        {
            ScanPath = dialog.FolderName;
        }
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        if (string.IsNullOrEmpty(ScanPath) || !Directory.Exists(ScanPath))
        {
            StatusMessage = "请选择有效的扫描路径";
            return;
        }

        await PerformScanAsync(false);
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanGlobalAsync()
    {
        await PerformScanAsync(true);
    }

    private async Task PerformScanAsync(bool isGlobal)
    {
        IsScanning = true;
        FoundSkills.Clear();
        _cts = new CancellationTokenSource();

        var progressReporter = new Progress<string>(msg =>
        {
            StatusMessage = msg;
        });

        try
        {
            ScanResult result;

            if (isGlobal)
            {
                StatusMessage = "正在进行全局扫描...";
                result = await _scannerService.ScanGlobalAsync(progressReporter, _cts.Token);
            }
            else
            {
                result = await _scannerService.ScanAsync(ScanPath, progressReporter, _cts.Token);
            }

            if (result.IsSuccess)
            {
                foreach (var skill in result.FoundSkills.OrderBy(s => s.Name))
                {
                    FoundSkills.Add(skill);
                }

                // 构建详细的统计信息
                var statsBuilder = new System.Text.StringBuilder();
                statsBuilder.Append($"扫描了 {result.ScannedDirectories:N0} 个目录，用时 {result.ElapsedTime.TotalSeconds:F2} 秒");
                if (result.SkippedDuplicateCount > 0 || result.SkippedLibraryCount > 0)
                {
                    statsBuilder.Append($" | 跳过: {result.SkippedDuplicateCount} 重复, {result.SkippedLibraryCount} 已存在");
                }
                ScanStats = statsBuilder.ToString();
                StatusMessage = $"找到 {result.FoundSkills.Count} 个可导入的技能";
            }
            else
            {
                StatusMessage = $"扫描失败: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"扫描出错: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private bool CanScan() => !IsScanning;

    [RelayCommand]
    private void CancelScan()
    {
        _cts?.Cancel();
        StatusMessage = "正在取消扫描...";
    }

    [RelayCommand]
    private async Task ImportSelectedAsync(SkillFolder? skill)
    {
        if (skill == null) return;

        var progress = new Progress<string>(msg => StatusMessage = msg);
        var success = await _libraryService.ImportSkillAsync(skill, progress);

        if (success)
        {
            FoundSkills.Remove(skill);
            StatusMessage = $"已导入: {skill.Name}";
        }
    }

    [RelayCommand]
    private async Task ImportAllAsync()
    {
        if (FoundSkills.Count == 0)
        {
            StatusMessage = "没有可导入的技能";
            return;
        }

        var skills = FoundSkills.ToList();
        var progress = new Progress<string>(msg => StatusMessage = msg);
        var count = await _libraryService.ImportSkillsAsync(skills, progress);

        // 刷新列表
        foreach (var skill in skills.Where(s => Directory.Exists(Path.Combine(_libraryService.LibraryPath, s.Name))))
        {
            FoundSkills.Remove(skill);
        }

        StatusMessage = $"成功导入 {count} 个技能";
    }

    [RelayCommand]
    private void OpenFolder(SkillFolder? skill)
    {
        if (skill != null)
        {
            _libraryService.OpenSkillFolder(skill);
        }
    }
}
