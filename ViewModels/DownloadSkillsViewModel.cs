using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkillManager.Models;
using SkillManager.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace SkillManager.ViewModels;

public partial class DownloadSkillsViewModel : ObservableObject
{
    private readonly DownloadLinksService _linksService;

    public DownloadSkillsViewModel(DownloadLinksService linksService)
    {
        _linksService = linksService;
        Links = new ObservableCollection<DownloadLinkItem>();
        Links.CollectionChanged += Links_CollectionChanged;
    }

    public ObservableCollection<DownloadLinkItem> Links { get; }

    [ObservableProperty]
    private string _newTitle = string.Empty;

    [ObservableProperty]
    private string _newUrl = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "添加下载链接后，可快速打开技能相关页面";

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private bool _hasSelection;

    [RelayCommand]
    public async Task LoadLinksAsync()
    {
        foreach (var link in Links)
        {
            link.PropertyChanged -= Link_PropertyChanged;
        }

        Links.Clear();
        var records = await _linksService.LoadLinksAsync();

        foreach (var record in records.OrderByDescending(r => r.CreatedTime))
        {
            Links.Add(new DownloadLinkItem
            {
                Title = record.Title,
                Url = record.Url,
                CreatedTime = record.CreatedTime
            });
        }

        StatusMessage = $"已加载 {Links.Count} 个链接";
        UpdateSelectionState();
    }

    [RelayCommand]
    public async Task AddLinkAsync()
    {
        var url = NormalizeUrl(NewUrl);
        if (string.IsNullOrWhiteSpace(url))
        {
            StatusMessage = "请输入有效的链接地址";
            return;
        }

        if (Links.Any(link => string.Equals(link.Url, url, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "链接已存在，无需重复添加";
            return;
        }

        var title = string.IsNullOrWhiteSpace(NewTitle) ? GetFallbackTitle(url) : NewTitle.Trim();
        var item = new DownloadLinkItem
        {
            Title = title,
            Url = url,
            CreatedTime = DateTime.Now
        };

        Links.Insert(0, item);
        await SaveLinksAsync();

        NewTitle = string.Empty;
        NewUrl = string.Empty;
        StatusMessage = $"已添加: {title}";
        UpdateSelectionState();
    }

    [RelayCommand]
    public void OpenLink(DownloadLinkItem? link)
    {
        if (link == null) return;

        var url = NormalizeUrl(link.Url);
        if (string.IsNullOrWhiteSpace(url))
        {
            StatusMessage = "链接地址无效";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = $"打开失败: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DeleteLinkAsync(DownloadLinkItem? link)
    {
        if (link == null) return;

        Links.Remove(link);
        await SaveLinksAsync();
        StatusMessage = $"已删除: {link.Title}";
        UpdateSelectionState();
    }

    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        var selected = Links.Where(link => link.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "没有选中的链接";
            return;
        }

        var result = MessageBox.Show(
            $"确定要删除选中的 {selected.Count} 个链接吗？",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        foreach (var link in selected)
        {
            Links.Remove(link);
        }

        await SaveLinksAsync();
        StatusMessage = $"已删除 {selected.Count} 个链接";
        UpdateSelectionState();
    }

    [RelayCommand]
    public void SelectAll()
    {
        foreach (var link in Links)
        {
            link.IsSelected = true;
        }

        UpdateSelectionState();
    }

    [RelayCommand]
    public void ClearSelection()
    {
        foreach (var link in Links)
        {
            link.IsSelected = false;
        }

        UpdateSelectionState();
    }

    private async Task SaveLinksAsync()
    {
        var records = Links.Select(link => new DownloadLinkRecord
        {
            Title = link.Title,
            Url = link.Url,
            CreatedTime = link.CreatedTime
        }).ToList();

        await _linksService.SaveLinksAsync(records);
    }

    private void Links_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (DownloadLinkItem item in e.NewItems)
            {
                item.PropertyChanged += Link_PropertyChanged;
            }
        }

        if (e.OldItems != null)
        {
            foreach (DownloadLinkItem item in e.OldItems)
            {
                item.PropertyChanged -= Link_PropertyChanged;
            }
        }

        UpdateSelectionState();
    }

    private void Link_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DownloadLinkItem.IsSelected))
        {
            UpdateSelectionState();
        }
    }

    private void UpdateSelectionState()
    {
        SelectedCount = Links.Count(link => link.IsSelected);
        HasSelection = SelectedCount > 0;
    }

    private static string NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;

        var trimmed = url.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            var fallback = $"https://{trimmed}";
            if (Uri.TryCreate(fallback, UriKind.Absolute, out var absoluteFallback))
            {
                return absoluteFallback.ToString();
            }
        }

        return string.Empty;
    }

    private static string GetFallbackTitle(string url)
    {
        try
        {
            var uri = new Uri(url);
            return string.IsNullOrWhiteSpace(uri.Host) ? url : uri.Host;
        }
        catch
        {
            return url;
        }
    }
}
