using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SkillManager.Models;
using SkillManager.Services;

namespace SkillManager.ViewModels;

public partial class AutomationViewModel : ObservableObject
{
    private readonly SkillAutomationService _automationService;
    private readonly SkillManagerSettingsService _settingsService;
    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;

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

    [ObservableProperty]
    private bool _isPollingEnabled;

    [ObservableProperty]
    private int _pollingIntervalSeconds = 10;

    [ObservableProperty]
    private string _nextPollTime = "";

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

        // 加载轮询间隔设置
        PollingIntervalSeconds = await _settingsService.LoadPollingIntervalAsync();

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

    [RelayCommand]
    public async Task UpdatePollingIntervalAsync(int newInterval)
    {
        PollingIntervalSeconds = Math.Max(5, Math.Min(300, newInterval));
        await _settingsService.SavePollingIntervalAsync(PollingIntervalSeconds);
        StatusMessage = $"轮询间隔已设置为 {PollingIntervalSeconds} 秒";

        // 如果正在轮询，重启以应用新间隔
        if (IsPollingEnabled)
        {
            await StopPollingAsync();
            await StartPollingAsync();
        }
    }

    [RelayCommand]
    public async Task StartPollingAsync()
    {
        if (IsPollingEnabled) return;

        if (WatchPaths.Count == 0)
        {
            await LoadWatchPathsAsync();
        }

        if (WatchPaths.Count == 0)
        {
            StatusMessage = "请先添加监控目录";
            return;
        }

        IsPollingEnabled = true;
        _pollingCts = new CancellationTokenSource();
        _pollingTask = RunPollingLoopAsync(_pollingCts.Token);
        StatusMessage = $"已启动轮询监控，间隔 {PollingIntervalSeconds} 秒";
    }

    [RelayCommand]
    public async Task StopPollingAsync()
    {
        if (!IsPollingEnabled) return;

        IsPollingEnabled = false;
        NextPollTime = "";

        if (_pollingCts != null)
        {
            await _pollingCts.CancelAsync();
            _pollingCts.Dispose();
            _pollingCts = null;
        }

        if (_pollingTask != null)
        {
            try
            {
                await _pollingTask;
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            _pollingTask = null;
        }

        StatusMessage = "已停止轮询监控";
    }

    private async Task RunPollingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 执行自动导入
                await RunAutoImportInternalAsync(true);

                // 计算下次轮询时间
                var nextPoll = DateTime.Now.AddSeconds(PollingIntervalSeconds);
                NextPollTime = $"下次轮询: {nextPoll:HH:mm:ss}";

                // 等待下一次轮询
                await Task.Delay(TimeSpan.FromSeconds(PollingIntervalSeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                StatusMessage = $"轮询出错: {ex.Message}";
                // 出错后等待一段时间再重试
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        NextPollTime = "";
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
            StatusMessage = IsPollingEnabled ? "轮询扫描中..." : "正在自动导入...";
            var result = await _automationService.RunAutoImportAsync(WatchPaths, progress);

            Logs.Clear();
            foreach (var log in result.Logs.OrderByDescending(item => item.Timestamp))
            {
                Logs.Add(log);
            }

            LastRunSummary = $"最近运行: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | 发现 {result.FoundSkills} | 导入 {result.ImportedCount} | 跳过 {result.SkippedCount}";
            StatusMessage = result.ImportedCount > 0
                ? $"已导入 {result.ImportedCount} 个技能"
                : (IsPollingEnabled ? $"轮询中，间隔 {PollingIntervalSeconds} 秒" : "未发现可导入的技能");

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
