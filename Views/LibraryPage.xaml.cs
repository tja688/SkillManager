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
/// 
/// 重要说明：
/// 此页面不包含自己的 ScrollViewer，因为 WPF UI 的 NavigationView 
/// 内部已有 ScrollViewer。嵌套 ScrollViewer 会导致内层 ScrollViewer 
/// 获得无限高度，从而 ScrollableHeight = 0，无法滚动。
/// 
/// 现在让 NavigationView 的内置 ScrollViewer 处理所有滚动操作。
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

        // 调试追踪
        SetupDebugTracking();
    }

    /// <summary>
    /// 设置调试追踪
    /// </summary>
    private void SetupDebugTracking()
    {
        // 追踪 ItemsControl 容器生成
        SkillsItemsControl.ItemContainerGenerator.StatusChanged += (s, e) =>
        {
            if (SkillsItemsControl.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
                {
                    _debugService.LogIfEnabled(
                        "scroll_scrollable_height",
                        "ItemsControl-Generated",
                        $"Containers generated - ItemsCount: {SkillsItemsControl.Items.Count}",
                        "SkillsItemsControl",
                        DebugLogLevel.Info);

                    // 卡片调试追踪
                    TrackCardDebugInfo();
                });
            }
        };

        // 追踪页面加载
        this.Loaded += (s, e) =>
        {
            _debugService.LogIfEnabled(
                "scroll_viewmodel_state",
                "Page-Loaded",
                "LibraryPage loaded - using NavigationView's built-in ScrollViewer for scrolling",
                "LibraryPage",
                DebugLogLevel.Info);

            // 页面加载后延迟追踪卡片（确保卡片已渲染）
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, TrackCardDebugInfo);
        };
    }

    /// <summary>
    /// 追踪卡片调试信息
    /// </summary>
    private void TrackCardDebugInfo()
    {
        // 检查资源解析
        _debugService.TrackResourceResolution(this, "CardBackgroundFillColorDefaultBrush");
        _debugService.TrackResourceResolution(this, "ControlStrokeColorDefaultBrush");
        _debugService.TrackResourceResolution(this, "AccentFillColorDefaultBrush");

        // 遍历所有卡片容器
        int cardCount = 0;
        foreach (var item in SkillsItemsControl.Items)
        {
            if (item is SkillFolder skill)
            {
                var container = SkillsItemsControl.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                if (container != null)
                {
                    // 查找 ui:Card 控件
                    var card = FindChild<Wpf.Ui.Controls.Card>(container);
                    if (card != null)
                    {
                        _debugService.TrackCardRender(card, skill.Name);
                        _debugService.TrackCardStyle(card, skill.Name);
                        _debugService.TrackCardLayout(card, skill.Name);
                        cardCount++;
                    }
                }
            }
        }

        // 输出卡片追踪汇总
        if (_debugService.IsOptionEnabled("card_render_tracking") || 
            _debugService.IsOptionEnabled("card_style_inspection"))
        {
            _debugService.Log("Card-Summary",
                $"Tracked {cardCount} cards out of {SkillsItemsControl.Items.Count} items",
                "LibraryPage",
                DebugLogLevel.Info);
        }
    }

    /// <summary>
    /// 在可视化树中查找指定类型的子元素
    /// </summary>
    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;

        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var foundChild = FindChild<T>(child);
            if (foundChild != null)
            {
                return foundChild;
            }
        }

        return null;
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
}
