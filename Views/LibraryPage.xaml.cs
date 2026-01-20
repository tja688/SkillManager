using SkillManager.Models;
using SkillManager.Services;
using SkillManager.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace SkillManager.Views;

/// <summary>
/// LibraryPage.xaml 的交互逻辑
/// </summary>
public partial class LibraryPage : Page
{
    public LibraryViewModel ViewModel { get; }
    private readonly DebugService _debugService;

    public LibraryPage(LibraryViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;
        _debugService = DebugService.Instance;

        InitializeComponent();

        // 注册滚轮事件处理器
        SkillsScrollViewer.AddHandler(
            PreviewMouseWheelEvent,
            new MouseWheelEventHandler(SkillsScrollViewer_PreviewMouseWheel),
            true);

        // 第二层：控件拦截检测 - 监控 ScrollViewer 状态变化
        SetupScrollViewerDebugTracking();
    }

    /// <summary>
    /// 设置 ScrollViewer 调试追踪
    /// </summary>
    private void SetupScrollViewerDebugTracking()
    {
        // 追踪 ScrollViewer 的滚动状态变化
        SkillsScrollViewer.ScrollChanged += (s, e) =>
        {
            if (_debugService.IsOptionEnabled("scroll_scrollable_height"))
            {
                _debugService.TrackScrollableHeight(
                    "SkillsScrollViewer",
                    SkillsScrollViewer.ScrollableHeight,
                    SkillsScrollViewer.VerticalOffset,
                    SkillsScrollViewer.ViewportHeight,
                    SkillsScrollViewer.ExtentHeight);
            }
        };

        // 追踪 ScrollViewer 获取焦点
        SkillsScrollViewer.GotFocus += (s, e) =>
        {
            _debugService.LogIfEnabled(
                "scroll_focus_tracking",
                "Focus",
                "SkillsScrollViewer GotFocus",
                e.OriginalSource?.GetType().Name ?? "unknown",
                DebugLogLevel.Event);
        };

        // 追踪 ScrollViewer 加载完成
        SkillsScrollViewer.Loaded += (s, e) =>
        {
            _debugService.LogIfEnabled(
                "scroll_scrollable_height",
                "ScrollViewer-Loaded",
                $"Initial State - ScrollableHeight: {SkillsScrollViewer.ScrollableHeight:F1}, VerticalOffset: {SkillsScrollViewer.VerticalOffset:F1}",
                "SkillsScrollViewer",
                DebugLogLevel.Info);
        };

        // 追踪 IsEnabled 变化（可能影响滚动）
        SkillsScrollViewer.IsEnabledChanged += (s, e) =>
        {
            _debugService.LogIfEnabled(
                "scroll_viewmodel_state",
                "ScrollViewer-IsEnabled",
                $"IsEnabled changed: {e.OldValue} -> {e.NewValue}",
                "SkillsScrollViewer",
                e.NewValue is false ? DebugLogLevel.Warning : DebugLogLevel.Info);
        };

        // 追踪 Visibility 变化
        SkillsScrollViewer.IsVisibleChanged += (s, e) =>
        {
            _debugService.LogIfEnabled(
                "scroll_viewmodel_state",
                "ScrollViewer-Visibility",
                $"IsVisible changed: {e.OldValue} -> {e.NewValue}",
                "SkillsScrollViewer",
                DebugLogLevel.Info);
        };
    }

    private void SkillCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (IsFromInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (sender is FrameworkElement element && element.DataContext is SkillFolder skill)
        {
            ViewModel.HandleCardClick(skill);
            e.Handled = true;
        }
    }

    private static bool IsFromInteractiveElement(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is ButtonBase || source is TextBoxBase)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void SkillsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            // 第二层：追踪滚轮事件处理
            _debugService.TrackScrollViewerWheel(sender, e, scrollViewer.ScrollableHeight, scrollViewer.VerticalOffset);

            // 检查并记录特殊情况
            if (scrollViewer.ScrollableHeight <= 0)
            {
                _debugService.LogIfEnabled(
                    "scroll_control_intercept",
                    "MouseWheel-Blocked",
                    $"ScrollableHeight <= 0, scroll event ignored. ScrollableHeight: {scrollViewer.ScrollableHeight:F1}",
                    "SkillsScrollViewer",
                    DebugLogLevel.Warning);
                return;
            }

            // 检查事件是否已被其他控件处理
            if (e.Handled)
            {
                _debugService.LogIfEnabled(
                    "scroll_control_intercept",
                    "MouseWheel-AlreadyHandled",
                    $"Event was already handled before reaching ScrollViewer. OriginalSource: {e.OriginalSource?.GetType().Name}",
                    "SkillsScrollViewer",
                    DebugLogLevel.Warning);
            }

            // 记录滚动前后的位置
            var oldOffset = scrollViewer.VerticalOffset;
            var newOffset = scrollViewer.VerticalOffset - e.Delta;

            _debugService.LogIfEnabled(
                "scroll_control_intercept",
                "MouseWheel-Scroll",
                $"Scrolling: {oldOffset:F1} -> {newOffset:F1} (Delta: {e.Delta})",
                "SkillsScrollViewer",
                DebugLogLevel.Event);

            scrollViewer.ScrollToVerticalOffset(newOffset);
            e.Handled = true;
        }
    }
}
