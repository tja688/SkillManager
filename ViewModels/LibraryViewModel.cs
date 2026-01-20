using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkillManager.Models;
using SkillManager.Services;
using SkillManager.Views;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace SkillManager.ViewModels;

/// <summary>
/// 技能库视图模型
/// </summary>
public partial class LibraryViewModel : ObservableObject
{
    private readonly LibraryService _libraryService;
    private readonly GroupService _groupService;
    private readonly DebugService _debugService;
    private readonly TranslationService _translationService;
    private readonly ManualTranslationStore _manualTranslationStore;
    private readonly string _translationMetaPath;
    private readonly List<SkillFolder> _allSkills = new();
    private readonly Dictionary<string, SkillFolder> _skillLookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _pendingTranslations = new(StringComparer.OrdinalIgnoreCase);
    private string? _pendingGroupId;
    private CancellationTokenSource? _pretranslateCts;
    private int _pendingTranslationCount;

    public LibraryViewModel(LibraryService libraryService, GroupService groupService, TranslationService translationService, ManualTranslationStore manualTranslationStore)
    {
        _libraryService = libraryService;
        _groupService = groupService;
        _translationService = translationService;
        _manualTranslationStore = manualTranslationStore;
        _debugService = DebugService.Instance;
        _translationMetaPath = Path.Combine(_libraryService.LibraryPath, ".translation_meta.json");
        
        Skills = new ObservableCollection<SkillFolder>();
        Groups = new ObservableCollection<SkillGroup>();
        FilteredSkills = new ObservableCollection<SkillFolder>();

        _isTranslationEnabled = _translationService.IsEnabled;

        _translationService.TranslationQueued += OnTranslationQueued;
        _translationService.TranslationCompleted += OnTranslationCompleted;
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

    [ObservableProperty]
    private bool _isTranslationRunning;

    [ObservableProperty]
    private string _translationStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBatchTranslationRunning;

    [ObservableProperty]
    private bool _isTranslationEnabled;

    partial void OnSelectedGroupChanged(SkillGroup? oldValue, SkillGroup? newValue)
    {
        // 第三层：ViewModel 状态追踪
        _debugService.TrackViewModelState(
            "LibraryViewModel",
            "SelectedGroup",
            oldValue?.Name ?? "null",
            newValue?.Name ?? "null");

        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        _debugService.TrackViewModelState(
            "LibraryViewModel",
            "SearchText",
            null,
            value);

        ApplyFilter();
    }

    partial void OnIsLoadingChanged(bool oldValue, bool newValue)
    {
        // 追踪 IsLoading 状态变化（可能影响 UI 交互）
        _debugService.TrackViewModelState(
            "LibraryViewModel",
            "IsLoading",
            oldValue,
            newValue);

        // 如果 IsLoading 为 true，可能会禁用某些控件
        if (newValue)
        {
            _debugService.LogIfEnabled(
                "scroll_viewmodel_state",
                "ViewModel-Loading",
                "IsLoading = true - UI交互可能受限",
                "LibraryViewModel",
                DebugLogLevel.Warning);
        }
    }

    partial void OnFilteredSkillsChanged(ObservableCollection<SkillFolder>? oldValue, ObservableCollection<SkillFolder> newValue)
    {
        _debugService.TrackViewModelState(
            "LibraryViewModel",
            "FilteredSkills.Count",
            oldValue?.Count ?? 0,
            newValue?.Count ?? 0);
    }

    partial void OnIsTranslationEnabledChanged(bool oldValue, bool newValue)
    {
        _translationService.SetEnabled(newValue);
        _ = SaveTranslationMetaAsync(newValue);

        if (!newValue)
        {
            _pretranslateCts?.Cancel();
            IsBatchTranslationRunning = false;
            TranslationStatusMessage = string.Empty;
            ClearPendingTranslationState();
            _ = ApplyTranslationsAsync(_allSkills);
            return;
        }

        if (_allSkills.Count > 0)
        {
            _ = ApplyTranslationsAsync(_allSkills);
            _ = _translationService.QueueIncrementalAsync(_allSkills, CancellationToken.None);
        }
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

            _debugService.LogIfEnabled(
                "scroll_viewmodel_state",
                "ViewModel-Refresh",
                "Starting skill refresh...",
                "LibraryViewModel",
                DebugLogLevel.Info);
            
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
            await ApplyTranslationsAsync(_allSkills);
            UpdateSkillLookup(_allSkills);
            if (IsTranslationEnabled)
            {
                _ = _translationService.QueueIncrementalAsync(_allSkills, CancellationToken.None);
            }

            _debugService.LogIfEnabled(
                "scroll_viewmodel_state",
                "ViewModel-Refresh",
                $"Loaded {_allSkills.Count} skills",
                "LibraryViewModel",
                DebugLogLevel.Info);

            ApplyFilter();
            GroupsRefreshed?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading skills: {ex.Message}");
            _debugService.LogIfEnabled(
                "scroll_viewmodel_state",
                "ViewModel-Error",
                $"Error loading skills: {ex.Message}",
                "LibraryViewModel",
                DebugLogLevel.Error);
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

    private async Task ApplyTranslationsAsync(IEnumerable<SkillFolder> skills)
    {
        var manualTranslations = await _manualTranslationStore.SyncAndLoadAsync(skills, CancellationToken.None);
        var cachedTranslations = IsTranslationEnabled
            ? await _translationService.GetCachedTranslationsAsync(skills, CancellationToken.None)
            : new Dictionary<string, TranslationPair>(StringComparer.OrdinalIgnoreCase);

        foreach (var skill in skills)
        {
            manualTranslations.TryGetValue(skill.SkillId, out var manualPair);
            cachedTranslations.TryGetValue(skill.SkillId, out var cachedPair);

            var whenToUse = manualPair?.WhenToUse ?? string.Empty;
            var description = manualPair?.Description ?? string.Empty;

            if (string.IsNullOrWhiteSpace(whenToUse) && !string.IsNullOrWhiteSpace(cachedPair?.WhenToUse))
            {
                whenToUse = cachedPair!.WhenToUse;
            }

            if (string.IsNullOrWhiteSpace(description) && !string.IsNullOrWhiteSpace(cachedPair?.Description))
            {
                description = cachedPair!.Description;
            }

            skill.WhenToUseZh = whenToUse;
            skill.DescriptionZh = description;
        }
    }

    private void UpdateSkillLookup(IEnumerable<SkillFolder> skills)
    {
        _skillLookup.Clear();
        _pendingTranslations.Clear();
        _pendingTranslationCount = 0;

        foreach (var skill in skills)
        {
            if (!string.IsNullOrWhiteSpace(skill.SkillId))
            {
                _skillLookup[skill.SkillId] = skill;
            }
        }

        UpdateTranslationIndicator();
    }

    private void ClearPendingTranslationState()
    {
        _pendingTranslations.Clear();
        _pendingTranslationCount = 0;

        foreach (var skill in _allSkills)
        {
            skill.IsTranslationPending = false;
            skill.TranslationStatusMessage = string.Empty;
        }

        UpdateTranslationIndicator();
    }

    private void UpdateTranslationIndicator()
    {
        if (!IsTranslationEnabled)
        {
            IsTranslationRunning = false;
            return;
        }

        IsTranslationRunning = IsBatchTranslationRunning || _pendingTranslationCount > 0;

        if (IsBatchTranslationRunning)
        {
            return;
        }

        if (_pendingTranslationCount > 0)
        {
            TranslationStatusMessage = $"后台翻译中：{_pendingTranslationCount} 项";
            return;
        }

        if (TranslationStatusMessage.StartsWith("后台翻译中", StringComparison.Ordinal))
        {
            TranslationStatusMessage = string.Empty;
        }
    }

    private async Task SaveTranslationMetaAsync(bool enabled)
    {
        TranslationMeta meta = new();

        if (File.Exists(_translationMetaPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_translationMetaPath);
                meta = JsonSerializer.Deserialize<TranslationMeta>(json) ?? new TranslationMeta();
            }
            catch
            {
                meta = new TranslationMeta();
            }
        }

        meta.DisableTranslation = !enabled;
        var output = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_translationMetaPath, output);
    }

    private void OnTranslationQueued(object? sender, TranslationQueuedEventArgs e)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (!IsTranslationEnabled)
            {
                return;
            }

            if (!_skillLookup.TryGetValue(e.SkillId, out var skill))
            {
                return;
            }

            if (!_pendingTranslations.TryGetValue(e.SkillId, out var count))
            {
                count = 0;
            }

            _pendingTranslations[e.SkillId] = count + 1;
            _pendingTranslationCount++;
            skill.IsTranslationPending = true;
            skill.TranslationStatusMessage = "翻译中...";
            UpdateTranslationIndicator();
        });
    }

    private void OnTranslationCompleted(object? sender, TranslationCompletedEventArgs e)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (!IsTranslationEnabled)
            {
                return;
            }

            if (!_skillLookup.TryGetValue(e.SkillId, out var skill))
            {
                return;
            }

            if (e.Success && !string.IsNullOrWhiteSpace(e.TranslatedText))
            {
                if (e.Field == TranslationFields.WhenToUse)
                {
                    skill.WhenToUseZh = e.TranslatedText;
                }
                else if (e.Field == TranslationFields.Description)
                {
                    skill.DescriptionZh = e.TranslatedText;
                }
            }

            if (_pendingTranslations.TryGetValue(e.SkillId, out var count))
            {
                count--;
                if (count <= 0)
                {
                    _pendingTranslations.Remove(e.SkillId);
                    skill.IsTranslationPending = false;
                    skill.TranslationStatusMessage = e.Success ? string.Empty : "翻译失败";
                }
                else
                {
                    _pendingTranslations[e.SkillId] = count;
                }
            }

            if (_pendingTranslationCount > 0)
            {
                _pendingTranslationCount--;
            }

            UpdateTranslationIndicator();
        });
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

        var filteredList = filtered.ToList();

        _debugService.LogIfEnabled(
            "scroll_viewmodel_state",
            "ViewModel-Filter",
            $"Applied filter: {filteredList.Count} skills matched (Group: {SelectedGroup?.Name ?? "All"}, Search: '{SearchText}')",
            "LibraryViewModel",
            DebugLogLevel.Info);

        FilteredSkills.Clear();
        foreach (var skill in filteredList)
        {
            FilteredSkills.Add(skill);
        }

        // 记录筛选后的状态（可能影响 ScrollViewer 的可滚动高度）
        _debugService.LogIfEnabled(
            "scroll_scrollable_height",
            "ViewModel-FilterComplete",
            $"FilteredSkills.Count = {FilteredSkills.Count} - ScrollViewer 内容已更新",
            "LibraryViewModel",
            DebugLogLevel.Info);
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
    public async Task StartPretranslate()
    {
        if (!IsTranslationEnabled) return;
        if (IsBatchTranslationRunning) return;

        IsBatchTranslationRunning = true;
        _pretranslateCts?.Cancel();
        _pretranslateCts = new CancellationTokenSource();
        TranslationStatusMessage = "准备批量预翻译...";
        UpdateTranslationIndicator();

        var progress = new Progress<TranslationProgressInfo>(info =>
        {
            TranslationStatusMessage = $"批量翻译：{info.Completed}/{info.Total}（失败 {info.Failed}）";
        });

        try
        {
            await _translationService.RunBatchPretranslateAsync(_allSkills, progress, _pretranslateCts.Token);
            if (!_pretranslateCts.Token.IsCancellationRequested)
            {
                TranslationStatusMessage = "批量翻译完成";
            }
        }
        catch (OperationCanceledException)
        {
            TranslationStatusMessage = "批量翻译已取消";
        }
        finally
        {
            IsBatchTranslationRunning = false;
            UpdateTranslationIndicator();
        }
    }

    [RelayCommand]
    public void CancelPretranslate()
    {
        _pretranslateCts?.Cancel();
    }

    [RelayCommand]
    public async Task ClearTranslationCache()
    {
        var result = MessageBox.Show(
            "确定要清空翻译缓存吗？此操作不会影响原始技能文件。",
            "清空翻译缓存",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        await _translationService.DeleteCacheAsync(CancellationToken.None);
        await ApplyTranslationsAsync(_allSkills);
        ClearPendingTranslationState();

        TranslationStatusMessage = "翻译缓存已清空";
        UpdateTranslationIndicator();
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
