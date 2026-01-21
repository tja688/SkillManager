using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SkillManager.Models;
using SkillManager.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace SkillManager.ViewModels;

public partial class CleanupViewModel : ObservableObject
{
    private readonly SkillScannerService _scannerService;
    private readonly SkillCleanupService _cleanupService;
    private readonly SkillManagerSettingsService _settingsService;
    private CancellationTokenSource? _cts;

    public CleanupViewModel(SkillScannerService scannerService, SkillCleanupService cleanupService, SkillManagerSettingsService settingsService)
    {
        _scannerService = scannerService;
        _cleanupService = cleanupService;
        _settingsService = settingsService;
        ProtectedPaths = new ObservableCollection<string>();
        FoundSkills = new ObservableCollection<SkillFolder>();
        FoundSkills.CollectionChanged += FoundSkills_CollectionChanged;
    }

    public ObservableCollection<string> ProtectedPaths { get; }

    public ObservableCollection<SkillFolder> FoundSkills { get; }

    [ObservableProperty]
    private string _statusMessage = "配置保护区后可开始全局清理扫描";

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanStats = string.Empty;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private bool _hasSelection;

    [RelayCommand]
    public async Task LoadProtectedPathsAsync()
    {
        ProtectedPaths.Clear();
        var paths = await _settingsService.LoadProtectedPathsAsync();

        foreach (var path in paths)
        {
            ProtectedPaths.Add(path);
        }

        StatusMessage = ProtectedPaths.Count > 0
            ? $"已加载 {ProtectedPaths.Count} 个保护区"
            : "尚未设置保护区";
    }

    [RelayCommand]
    public async Task AddProtectedPathAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择保护区目录"
        };

        if (dialog.ShowDialog() != true) return;

        var normalized = PathUtilities.NormalizePath(dialog.FolderName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            StatusMessage = "选择的路径无效";
            return;
        }

        if (ProtectedPaths.Any(path => string.Equals(path, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "该保护区已存在";
            return;
        }

        ProtectedPaths.Add(normalized);
        await _settingsService.SaveProtectedPathsAsync(ProtectedPaths);
        StatusMessage = $"已添加保护区: {normalized}";
    }

    [RelayCommand]
    public async Task RemoveProtectedPathAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        ProtectedPaths.Remove(path);
        await _settingsService.SaveProtectedPathsAsync(ProtectedPaths);
        StatusMessage = $"已移除保护区: {path}";
    }

    [RelayCommand]
    public async Task ScanGlobalAsync()
    {
        if (IsScanning) return;

        IsScanning = true;
        foreach (var skill in FoundSkills)
        {
            skill.PropertyChanged -= Skill_PropertyChanged;
        }

        FoundSkills.Clear();
        _cts = new CancellationTokenSource();

        var progress = new Progress<string>(msg => StatusMessage = msg);
        var protectedPaths = PathUtilities.NormalizePaths(ProtectedPaths);

        try
        {
            StatusMessage = "开始全局扫描...";
            var result = await _scannerService.ScanGlobalAsync(progress, _cts.Token, protectedPaths);

            if (result.IsSuccess)
            {
                foreach (var skill in result.FoundSkills.OrderBy(s => s.Name))
                {
                    FoundSkills.Add(skill);
                }

                ScanStats = $"扫描 {result.ScannedDirectories:N0} 个目录，用时 {result.ElapsedTime.TotalSeconds:F2} 秒";
                StatusMessage = $"找到 {result.FoundSkills.Count} 个可清理技能";
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

    [RelayCommand]
    private void CancelScan()
    {
        _cts?.Cancel();
        StatusMessage = "正在取消扫描...";
    }

    [RelayCommand]
    public void SelectAll()
    {
        foreach (var skill in FoundSkills)
        {
            skill.IsSelected = true;
        }

        UpdateSelectionState();
    }

    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        var selected = FoundSkills.Where(skill => skill.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "没有选中的技能";
            return;
        }

        var result = MessageBox.Show(
            $"确定要删除选中的 {selected.Count} 个技能吗？",
            "确认清理",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        await DeleteSkillsAsync(selected);
    }

    [RelayCommand]
    public async Task DeleteAllAsync()
    {
        if (FoundSkills.Count == 0)
        {
            StatusMessage = "暂无可清理的技能";
            return;
        }

        var result = MessageBox.Show(
            $"确定要一键清理当前 {FoundSkills.Count} 个技能吗？",
            "确认清理",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        await DeleteSkillsAsync(FoundSkills.ToList());
    }

    [RelayCommand]
    public void OpenFolder(SkillFolder? skill)
    {
        if (skill == null) return;

        if (Directory.Exists(skill.FullPath))
        {
            Process.Start("explorer.exe", skill.FullPath);
        }
    }

    private async Task DeleteSkillsAsync(List<SkillFolder> skills)
    {
        var protectedPaths = PathUtilities.NormalizePaths(ProtectedPaths);
        var deleted = new List<SkillFolder>();
        var progress = new Progress<string>(msg => StatusMessage = msg);

        foreach (var skill in skills)
        {
            if (await _cleanupService.DeleteSkillAsync(skill, protectedPaths, progress))
            {
                deleted.Add(skill);
            }
        }

        foreach (var skill in deleted)
        {
            FoundSkills.Remove(skill);
        }

        StatusMessage = $"已清理 {deleted.Count} 个技能";
        UpdateSelectionState();
    }

    private void FoundSkills_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (SkillFolder item in e.NewItems)
            {
                item.PropertyChanged += Skill_PropertyChanged;
            }
        }

        if (e.OldItems != null)
        {
            foreach (SkillFolder item in e.OldItems)
            {
                item.PropertyChanged -= Skill_PropertyChanged;
            }
        }

        UpdateSelectionState();
    }

    private void Skill_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SkillFolder.IsSelected))
        {
            UpdateSelectionState();
        }
    }

    private void UpdateSelectionState()
    {
        SelectedCount = FoundSkills.Count(skill => skill.IsSelected);
        HasSelection = SelectedCount > 0;
    }
}
