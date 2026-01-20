using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SkillManager.Services;

/// <summary>
/// 调试日志条目
/// </summary>
public class DebugLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DebugLogLevel Level { get; set; }
    
    /// <summary>
    /// 格式化后的显示文本（用于复制）
    /// </summary>
    public string DisplayText => $"[{Timestamp:HH:mm:ss.fff}] [{Level}] [{Category}] {Message} | Source: {Source}";
}

/// <summary>
/// 调试日志级别
/// </summary>
public enum DebugLogLevel
{
    Info,
    Warning,
    Error,
    Event
}

/// <summary>
/// 调试选项配置
/// </summary>
public partial class DebugOption : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _category = string.Empty;
}

/// <summary>
/// 调试服务 - 单例模式，管理调试选项和日志
/// </summary>
public class DebugService
{
    private static readonly Lazy<DebugService> _instance = new(() => new DebugService());
    public static DebugService Instance => _instance.Value;

    private readonly ConcurrentQueue<DebugLogEntry> _logBuffer = new();
    private readonly object _logLock = new();
    private const int MaxLogEntries = 500;

    public ObservableCollection<DebugLogEntry> Logs { get; } = new();
    public ObservableCollection<DebugOption> DebugOptions { get; } = new();

    public event Action<DebugLogEntry>? LogAdded;

    private DebugService()
    {
        InitializeDebugOptions();
    }

    private void InitializeDebugOptions()
    {
        // WPF ScrollViewer Focus/Hitching Issue 调试选项
        DebugOptions.Add(new DebugOption
        {
            Id = "scroll_global_routing",
            Name = "全局鼠标滚轮路由追踪",
            Description = "在 MainWindow 追踪 PreviewMouseWheel 事件，监控滚轮事件是否被触发及触发源",
            Category = "ScrollViewer 调试"
        });

        DebugOptions.Add(new DebugOption
        {
            Id = "scroll_control_intercept",
            Name = "控件滚轮拦截检测",
            Description = "在 LibraryPage 检测 ScrollViewer 的滚轮事件处理情况",
            Category = "ScrollViewer 调试"
        });

        DebugOptions.Add(new DebugOption
        {
            Id = "scroll_viewmodel_state",
            Name = "ViewModel 状态检查",
            Description = "追踪 LibraryViewModel 的 IsLoading 等状态变化",
            Category = "ScrollViewer 调试"
        });

        DebugOptions.Add(new DebugOption
        {
            Id = "scroll_visual_tree",
            Name = "可视化树结构追踪",
            Description = "分析滚轮事件触发时的可视化树层级结构",
            Category = "ScrollViewer 调试"
        });

        DebugOptions.Add(new DebugOption
        {
            Id = "scroll_focus_tracking",
            Name = "焦点状态追踪",
            Description = "追踪键盘焦点和鼠标焦点的变化",
            Category = "ScrollViewer 调试"
        });

        DebugOptions.Add(new DebugOption
        {
            Id = "scroll_scrollable_height",
            Name = "ScrollViewer 可滚动高度追踪",
            Description = "追踪 ScrollViewer 的 ScrollableHeight 和 VerticalOffset 变化",
            Category = "ScrollViewer 调试"
        });

        // 卡片样式调试选项
        DebugOptions.Add(new DebugOption
        {
            Id = "card_render_tracking",
            Name = "卡片渲染追踪",
            Description = "追踪 ui:Card 控件的 Loaded 事件，检测卡片是否正确渲染",
            Category = "卡片样式调试"
        });

        DebugOptions.Add(new DebugOption
        {
            Id = "card_style_inspection",
            Name = "卡片样式检查",
            Description = "检查卡片的 Background、BorderBrush 等样式属性的实际值",
            Category = "卡片样式调试"
        });

        DebugOptions.Add(new DebugOption
        {
            Id = "card_layout_tracking",
            Name = "卡片布局追踪",
            Description = "追踪卡片的 ActualWidth、ActualHeight、Margin、Padding 等布局属性",
            Category = "卡片样式调试"
        });

        DebugOptions.Add(new DebugOption
        {
            Id = "card_resource_resolution",
            Name = "卡片资源解析追踪",
            Description = "检查 DynamicResource 是否正确解析为 Brush 对象",
            Category = "卡片样式调试"
        });
    }

    /// <summary>
    /// 检查指定调试选项是否启用
    /// </summary>
    public bool IsOptionEnabled(string optionId)
    {
        return DebugOptions.FirstOrDefault(o => o.Id == optionId)?.IsEnabled ?? false;
    }

    /// <summary>
    /// 设置调试选项状态
    /// </summary>
    public void SetOptionEnabled(string optionId, bool enabled)
    {
        var option = DebugOptions.FirstOrDefault(o => o.Id == optionId);
        if (option != null)
        {
            option.IsEnabled = enabled;
        }
    }

    /// <summary>
    /// 记录调试日志
    /// </summary>
    public void Log(string category, string message, string source = "", DebugLogLevel level = DebugLogLevel.Info)
    {
        var entry = new DebugLogEntry
        {
            Timestamp = DateTime.Now,
            Category = category,
            Message = message,
            Source = source,
            Level = level
        };

        // 添加到缓冲区
        _logBuffer.Enqueue(entry);

        // 在 UI 线程更新
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            lock (_logLock)
            {
                Logs.Add(entry);

                // 限制日志数量
                while (Logs.Count > MaxLogEntries)
                {
                    Logs.RemoveAt(0);
                }
            }

            LogAdded?.Invoke(entry);
        });

        // 同时输出到 Debug 窗口
        Debug.WriteLine(entry.DisplayText);
    }

    /// <summary>
    /// 条件日志 - 仅当指定选项启用时记录
    /// </summary>
    public void LogIfEnabled(string optionId, string category, string message, string source = "", DebugLogLevel level = DebugLogLevel.Info)
    {
        if (IsOptionEnabled(optionId))
        {
            Log(category, message, source, level);
        }
    }

    /// <summary>
    /// 清空日志
    /// </summary>
    public void ClearLogs()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            lock (_logLock)
            {
                Logs.Clear();
            }
        });
    }

    /// <summary>
    /// 获取所有日志的纯文本（用于复制）
    /// </summary>
    public string GetLogsAsText()
    {
        lock (_logLock)
        {
            return string.Join(Environment.NewLine, Logs.Select(l => l.DisplayText));
        }
    }

    /// <summary>
    /// 获取指定类别的日志
    /// </summary>
    public IEnumerable<DebugLogEntry> GetLogsByCategory(string category)
    {
        lock (_logLock)
        {
            return Logs.Where(l => l.Category == category).ToList();
        }
    }

    #region 滚轮调试辅助方法

    /// <summary>
    /// 追踪全局滚轮事件
    /// </summary>
    public void TrackGlobalMouseWheel(MouseWheelEventArgs e, IInputElement? directlyOver)
    {
        if (!IsOptionEnabled("scroll_global_routing")) return;

        var overType = directlyOver?.GetType().Name ?? "null";
        var originalSourceType = e.OriginalSource?.GetType().Name ?? "null";
        var sourceType = e.Source?.GetType().Name ?? "null";

        Log("MouseWheel-Global",
            $"Delta: {e.Delta}, Handled: {e.Handled}, DirectlyOver: {overType}, OriginalSource: {originalSourceType}",
            $"Source: {sourceType}",
            DebugLogLevel.Event);
    }

    /// <summary>
    /// 追踪控件滚轮拦截
    /// </summary>
    public void TrackScrollViewerWheel(object sender, MouseWheelEventArgs e, double scrollableHeight, double verticalOffset)
    {
        if (!IsOptionEnabled("scroll_control_intercept")) return;

        var senderType = sender?.GetType().Name ?? "null";

        Log("MouseWheel-ScrollViewer",
            $"Delta: {e.Delta}, Handled: {e.Handled}, ScrollableHeight: {scrollableHeight:F1}, VerticalOffset: {verticalOffset:F1}",
            senderType,
            e.Handled ? DebugLogLevel.Warning : DebugLogLevel.Event);
    }

    /// <summary>
    /// 追踪 ViewModel 状态变化
    /// </summary>
    public void TrackViewModelState(string viewModelName, string propertyName, object? oldValue, object? newValue)
    {
        if (!IsOptionEnabled("scroll_viewmodel_state")) return;

        Log("ViewModel-State",
            $"{propertyName}: {oldValue} -> {newValue}",
            viewModelName,
            DebugLogLevel.Info);
    }

    /// <summary>
    /// 追踪可视化树结构
    /// </summary>
    public void TrackVisualTree(DependencyObject element, string context)
    {
        if (!IsOptionEnabled("scroll_visual_tree")) return;

        var hierarchy = GetVisualTreeHierarchy(element);
        Log("VisualTree",
            $"Context: {context}\nHierarchy: {hierarchy}",
            element.GetType().Name,
            DebugLogLevel.Info);
    }

    /// <summary>
    /// 追踪焦点变化
    /// </summary>
    public void TrackFocusChange(string focusType, IInputElement? oldFocus, IInputElement? newFocus)
    {
        if (!IsOptionEnabled("scroll_focus_tracking")) return;

        var oldType = oldFocus?.GetType().Name ?? "null";
        var newType = newFocus?.GetType().Name ?? "null";

        Log("Focus",
            $"{focusType}: {oldType} -> {newType}",
            "",
            DebugLogLevel.Event);
    }

    /// <summary>
    /// 追踪 ScrollViewer 可滚动高度
    /// </summary>
    public void TrackScrollableHeight(string scrollViewerName, double scrollableHeight, double verticalOffset, double viewportHeight, double extentHeight)
    {
        if (!IsOptionEnabled("scroll_scrollable_height")) return;

        Log("ScrollViewer-Metrics",
            $"ScrollableHeight: {scrollableHeight:F1}, VerticalOffset: {verticalOffset:F1}, ViewportHeight: {viewportHeight:F1}, ExtentHeight: {extentHeight:F1}",
            scrollViewerName,
            DebugLogLevel.Info);
    }

    private static string GetVisualTreeHierarchy(DependencyObject element, int maxDepth = 5)
    {
        var parts = new List<string>();
        var current = element;
        var depth = 0;

        while (current != null && depth < maxDepth)
        {
            parts.Add(current.GetType().Name);
            current = VisualTreeHelper.GetParent(current);
            depth++;
        }

        if (current != null)
        {
            parts.Add("...");
        }

        parts.Reverse();
        return string.Join(" > ", parts);
    }

    #endregion

    #region 卡片样式调试辅助方法

    /// <summary>
    /// 追踪卡片渲染事件
    /// </summary>
    public void TrackCardRender(FrameworkElement card, string skillName)
    {
        if (!IsOptionEnabled("card_render_tracking")) return;

        var actualWidth = card.ActualWidth;
        var actualHeight = card.ActualHeight;
        var isLoaded = card.IsLoaded;
        var visibility = card.Visibility;

        Log("Card-Render",
            $"Skill: {skillName}, IsLoaded: {isLoaded}, Visibility: {visibility}, Size: {actualWidth:F0}x{actualHeight:F0}",
            card.GetType().Name,
            DebugLogLevel.Event);
    }

    /// <summary>
    /// 检查卡片样式属性
    /// </summary>
    public void TrackCardStyle(System.Windows.Controls.Control card, string skillName)
    {
        if (!IsOptionEnabled("card_style_inspection")) return;

        var background = card.Background;
        var borderBrush = card.BorderBrush;
        var borderThickness = card.BorderThickness;

        string bgInfo = GetBrushInfo(background);
        string borderInfo = GetBrushInfo(borderBrush);

        Log("Card-Style",
            $"Skill: {skillName}\n  Background: {bgInfo}\n  BorderBrush: {borderInfo}\n  BorderThickness: {borderThickness}",
            card.GetType().Name,
            background == null ? DebugLogLevel.Warning : DebugLogLevel.Info);
    }

    /// <summary>
    /// 追踪卡片布局
    /// </summary>
    public void TrackCardLayout(FrameworkElement card, string skillName)
    {
        if (!IsOptionEnabled("card_layout_tracking")) return;

        var margin = card.Margin;
        var actualWidth = card.ActualWidth;
        var actualHeight = card.ActualHeight;
        var renderTransform = card.RenderTransform;

        string transformInfo = "None";
        if (renderTransform != null && renderTransform != Transform.Identity)
        {
            transformInfo = renderTransform.GetType().Name;
        }

        Log("Card-Layout",
            $"Skill: {skillName}, Size: {actualWidth:F0}x{actualHeight:F0}, Margin: {margin}, Transform: {transformInfo}",
            card.GetType().Name,
            DebugLogLevel.Info);
    }

    /// <summary>
    /// 检查资源解析
    /// </summary>
    public void TrackResourceResolution(FrameworkElement element, string resourceName)
    {
        if (!IsOptionEnabled("card_resource_resolution")) return;

        object? resource = null;
        try
        {
            resource = element.TryFindResource(resourceName);
        }
        catch (Exception ex)
        {
            Log("Card-ResourceResolution",
                $"Resource: {resourceName} - Exception: {ex.Message}",
                element.GetType().Name,
                DebugLogLevel.Error);
            return;
        }

        if (resource == null)
        {
            Log("Card-ResourceResolution",
                $"Resource: {resourceName} - NOT FOUND (null)",
                element.GetType().Name,
                DebugLogLevel.Warning);
        }
        else
        {
            string brushInfo = resource is System.Windows.Media.Brush brush ? GetBrushInfo(brush) : resource.GetType().Name;
            Log("Card-ResourceResolution",
                $"Resource: {resourceName} - Resolved: {brushInfo}",
                element.GetType().Name,
                DebugLogLevel.Info);
        }
    }

    /// <summary>
    /// 获取画刷详细信息
    /// </summary>
    private static string GetBrushInfo(System.Windows.Media.Brush? brush)
    {
        if (brush == null)
            return "NULL";

        if (brush is SolidColorBrush scb)
        {
            var color = scb.Color;
            return $"SolidColorBrush(#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}, Opacity={scb.Opacity:F2})";
        }
        else if (brush is LinearGradientBrush lgb)
        {
            return $"LinearGradientBrush(Stops={lgb.GradientStops.Count}, Opacity={lgb.Opacity:F2})";
        }
        else if (brush is RadialGradientBrush rgb)
        {
            return $"RadialGradientBrush(Stops={rgb.GradientStops.Count}, Opacity={rgb.Opacity:F2})";
        }

        return $"{brush.GetType().Name}(Opacity={brush.Opacity:F2})";
    }

    #endregion
}
