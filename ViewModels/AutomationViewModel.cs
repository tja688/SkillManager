using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SkillManager.Models;
using SkillManager.Services;
using System.Collections.ObjectModel;

namespace SkillManager.ViewModels;

public partial class AutomationViewModel : ObservableObject
{
    private readonly SkillAutomationService _automationService;
    private readonly SkillManagerSettingsService _settingsService;

    public AutomationViewModel(SkillAutomationService automationService, SkillManagerSettingsService settingsService)
    {
        _automationService = automationService;
        _settingsService = settingsService;
        WatchPaths = new ObservableCollection<string>();
        Logs = new ObservableCollection<AutomationLogItem>();
    }

    public ObservableCollection<string> WatchPaths { get; }

    public ObservableCollection<AutomationLogItem> Logs { get; }

    [ObservableProperty]
    private string _statusMessage = "等待自动导入";

    [ObservableProperty]
    private string _lastRunSummary = "尚未运行";

    [ObservableProperty]
    private bool _isRunning;

    public event Action? AutomationCompleted;

    [RelayCommand]
    public async Task LoadWatchPathsAsync()
    {
        WatchPaths.Clear();
        var paths = await _settingsService.LoadAutomationPathsAsync();

        foreach (var path in paths)
        {
            WatchPaths.Add(path);
        }

        StatusMessage = WatchPaths.Count > 0
            ? $"已加载 {WatchPaths.Count} 个监控目录"
            : "尚未配置监控目录";
    }

    [RelayCommand]
    public async Task AddWatchPathAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择自动化监控目录"
        };

        if (dialog.ShowDialog() != true) return;

        var normalized = PathUtilities.NormalizePath(dialog.FolderName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            StatusMessage = "选择的路径无效";
            return;
        }

        if (WatchPaths.Any(path => string.Equals(path, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "该目录已在监控列表中";
            return;
        }

        WatchPaths.Add(normalized);
        await _settingsService.SaveAutomationPathsAsync(WatchPaths);
        StatusMessage = $"已添加监控目录: {normalized}";
    }

    [RelayCommand]
    public async Task RemoveWatchPathAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        WatchPaths.Remove(path);
        await _settingsService.SaveAutomationPathsAsync(WatchPaths);
        StatusMessage = $"已移除监控目录: {path}";
    }

    [RelayCommand]
    public async Task RunAutoImportAsync()
    {
        await RunAutoImportInternalAsync(false);
    }

    public async Task RunAutoImportOnStartupAsync()
    {
        await RunAutoImportInternalAsync(true);
    }

    [RelayCommand]
    public void ClearLogs()
    {
        Logs.Clear();
    }

    private async Task RunAutoImportInternalAsync(bool isStartup)
    {
        if (IsRunning) return;

        if (WatchPaths.Count == 0)
        {
            await LoadWatchPathsAsync();
        }

        if (WatchPaths.Count == 0)
        {
            if (!isStartup)
            {
                StatusMessage = "请先添加监控目录";
            }
            return;
        }

        IsRunning = true;
        var progress = new Progress<string>(msg => StatusMessage = msg);

        try
        {
            StatusMessage = "正在自动导入...";
            var result = await _automationService.RunAutoImportAsync(WatchPaths, progress);

            Logs.Clear();
            foreach (var log in result.Logs.OrderByDescending(item => item.Timestamp))
            {
                Logs.Add(log);
            }

            LastRunSummary = $"最近运行: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | 发现 {result.FoundSkills} | 导入 {result.ImportedCount} | 跳过 {result.SkippedCount}";
            StatusMessage = result.ImportedCount > 0
                ? $"已导入 {result.ImportedCount} 个技能"
                : "未发现可导入的技能";

            AutomationCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"自动导入失败: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }
}
