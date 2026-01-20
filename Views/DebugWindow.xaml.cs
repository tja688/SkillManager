using SkillManager.Services;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace SkillManager.Views;

/// <summary>
/// DebugWindow.xaml 的交互逻辑
/// </summary>
public partial class DebugWindow : FluentWindow
{
    private readonly DebugService _debugService;
    private readonly CollectionViewSource _logsViewSource;

    public DebugWindow()
    {
        InitializeComponent();

        _debugService = DebugService.Instance;

        // 绑定调试选项
        DebugOptionsItemsControl.ItemsSource = _debugService.DebugOptions;

        // 设置日志视图源（用于筛选）
        _logsViewSource = new CollectionViewSource { Source = _debugService.Logs };
        _logsViewSource.Filter += LogsViewSource_Filter;
        LogListView.ItemsSource = _logsViewSource.View;

        // 监听日志变化
        _debugService.Logs.CollectionChanged += Logs_CollectionChanged;
        _debugService.LogAdded += DebugService_LogAdded;

        UpdateLogCount();
    }

    private void LogsViewSource_Filter(object sender, FilterEventArgs e)
    {
        if (e.Item is not DebugLogEntry log)
        {
            e.Accepted = false;
            return;
        }

        var filterText = FilterTextBox.Text;
        if (string.IsNullOrWhiteSpace(filterText))
        {
            e.Accepted = true;
            return;
        }

        e.Accepted = log.DisplayText.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    private void Logs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateLogCount();
    }

    private void DebugService_LogAdded(DebugLogEntry entry)
    {
        // 自动滚动到底部
        if (AutoScrollToggle.IsChecked == true && LogListView.Items.Count > 0)
        {
            LogListView.ScrollIntoView(LogListView.Items[LogListView.Items.Count - 1]);
        }
    }

    private void UpdateLogCount()
    {
        Dispatcher.BeginInvoke(() =>
        {
            LogCountText.Text = $" ({_debugService.Logs.Count} 条)";
        });
    }

    private void EnableAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var option in _debugService.DebugOptions)
        {
            option.IsEnabled = true;
        }
        _debugService.Log("Debug", "已启用全部调试选项", "DebugWindow", DebugLogLevel.Info);
    }

    private void DisableAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var option in _debugService.DebugOptions)
        {
            option.IsEnabled = false;
        }
        _debugService.Log("Debug", "已禁用全部调试选项", "DebugWindow", DebugLogLevel.Info);
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        _debugService.ClearLogs();
        StatusText.Text = "✅ 日志已清空";
    }

    private void CopyAllLogs_Click(object sender, RoutedEventArgs e)
    {
        var text = _debugService.GetLogsAsText();
        if (string.IsNullOrEmpty(text))
        {
            StatusText.Text = "⚠️ 没有日志可复制";
            return;
        }

        try
        {
            Clipboard.SetText(text);
            StatusText.Text = $"✅ 已复制 {_debugService.Logs.Count} 条日志到剪贴板";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"❌ 复制失败: {ex.Message}";
        }
    }

    private void CopySelectedLogs_Click(object sender, RoutedEventArgs e)
    {
        var selectedLogs = LogListView.SelectedItems.Cast<DebugLogEntry>().ToList();
        if (selectedLogs.Count == 0)
        {
            StatusText.Text = "⚠️ 请先选择要复制的日志";
            return;
        }

        var text = string.Join(Environment.NewLine, selectedLogs.Select(l => l.DisplayText));

        try
        {
            Clipboard.SetText(text);
            StatusText.Text = $"✅ 已复制 {selectedLogs.Count} 条日志到剪贴板";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"❌ 复制失败: {ex.Message}";
        }
    }

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _logsViewSource.View.Refresh();

        var visibleCount = _logsViewSource.View.Cast<object>().Count();
        StatusText.Text = $"🔍 筛选结果: {visibleCount} / {_debugService.Logs.Count} 条日志";
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _logsViewSource.View.Refresh();
        StatusText.Text = "🔄 视图已刷新";
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _debugService.Logs.CollectionChanged -= Logs_CollectionChanged;
        _debugService.LogAdded -= DebugService_LogAdded;
        base.OnClosed(e);
    }
}
