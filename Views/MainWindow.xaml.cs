using SkillManager.Models;
using SkillManager.Services;
using SkillManager.ViewModels;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace SkillManager.Views;

/// <summary>
/// MainWindow.xaml 的交互逻辑
/// </summary>
public partial class MainWindow : FluentWindow
{
    public MainWindowViewModel ViewModel { get; }
    private readonly PageService _pageService;
    private readonly DebugService _debugService;
    private Project? _currentDetailProject;
    private DebugWindow? _debugWindow;

    public MainWindow()
    {
        ViewModel = new MainWindowViewModel();
        DataContext = ViewModel;
        _debugService = DebugService.Instance;

        InitializeComponent();

        // 设置导航服务
        _pageService = new PageService(this, ViewModel);
        NavigationView.SetPageService(_pageService);

        ViewModel.LibraryViewModel.GroupsRefreshed += RefreshLibraryNavigationGroups;

        // 注册全局滚轮事件追踪（调试用）
        SetupDebugEventTracking();

        // 默认导航到Library页面
        Loaded += (s, e) =>
        {
            RefreshLibraryNavigationGroups();
            RefreshLibraryNavigationGroups();
            NavigationView.Navigate(typeof(AllSkillsPage));
        };
    }

    /// <summary>
    /// 设置调试事件追踪
    /// </summary>
    private void SetupDebugEventTracking()
    {
        // 第一层：全局鼠标滚轮路由追踪
        this.PreviewMouseWheel += (s, e) =>
        {
            var hoveredElement = Mouse.DirectlyOver;
            _debugService.TrackGlobalMouseWheel(e, hoveredElement);
        };

        // 焦点追踪
        this.GotKeyboardFocus += (s, e) =>
        {
            _debugService.TrackFocusChange("KeyboardFocus", e.OldFocus, e.NewFocus);
        };

        this.LostKeyboardFocus += (s, e) =>
        {
            _debugService.TrackFocusChange("KeyboardFocus-Lost", e.OldFocus, e.NewFocus);
        };

        // 鼠标移动时追踪可视化树（限制频率）
        System.DateTime lastVisualTreeTrack = System.DateTime.MinValue;
        this.PreviewMouseMove += (s, e) =>
        {
            if (_debugService.IsOptionEnabled("scroll_visual_tree"))
            {
                var now = System.DateTime.Now;
                if ((now - lastVisualTreeTrack).TotalSeconds >= 2) // 每2秒最多记录一次
                {
                    var element = e.OriginalSource as DependencyObject;
                    if (element != null)
                    {
                        _debugService.TrackVisualTree(element, "MouseMove");
                        lastVisualTreeTrack = now;
                    }
                }
            }
        };
    }

    /// <summary>
    /// 打开调试窗口
    /// </summary>
    private void DebugButton_Click(object sender, RoutedEventArgs e)
    {
        if (_debugWindow == null || !_debugWindow.IsLoaded)
        {
            _debugWindow = new DebugWindow
            {
                Owner = this
            };
            _debugWindow.Closed += (s, args) => _debugWindow = null;
            _debugWindow.Show();
        }
        else
        {
            _debugWindow.Activate();
        }
    }

    /// <summary>
    /// 导航到项目详情页面
    /// </summary>
    public void NavigateToProjectDetail(Project project)
    {
        _currentDetailProject = project;
        var detailViewModel = ViewModel.CreateProjectDetailViewModel(() => NavigateBackToProjectList());
        var detailPage = new ProjectDetailPage(detailViewModel);
        
        // 异步加载项目
        _ = detailViewModel.LoadProjectAsync(project);
        
        // 存储页面实例供PageService使用
        _pageService.SetProjectDetailPage(detailPage);
        
        // 导航到详情页面类型
        NavigationView.Navigate(typeof(ProjectDetailPage));
    }

    /// <summary>
    /// 返回项目列表
    /// </summary>
    public void NavigateBackToProjectList()
    {
        _currentDetailProject = null;
        _pageService.SetProjectDetailPage(null);
        NavigationView.Navigate(typeof(ProjectListPage));
    }

    private void NavigationView_SelectionChanged(NavigationView navigationView, RoutedEventArgs e)
    {
        if (navigationView.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        // Child Check (Groups) - TargetPageType: LibraryPage
        if (LibraryNavItem.MenuItems.Contains(item))
        {
             var groupId = item.Tag as string;
             ViewModel.LibraryViewModel.SelectGroupById(groupId);
             // No manual navigation needed, TargetPageType handles it
             // And since type is LibraryPage != AllSkillsPage, Parent won't be auto-selected
             return;
        }
        
        // Parent Check ("Skill Library") - TargetPageType: AllSkillsPage
        if (item == LibraryNavItem)
        {
             // When this is selected (either mainly or auto if we are on AllSkillsPage),
             // we want to show all skills.
             ViewModel.LibraryViewModel.SelectGroupById(string.Empty);
        }
    }

    private void LibraryNavItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var clickedItem = FindAncestorNavigationItem(e.OriginalSource as DependencyObject);
        if (clickedItem != null && !ReferenceEquals(clickedItem, sender))
        {
            return;
        }

        // Parent Click: Just ensure we go to AllSkillsPage which selects Parent
        // SelectionChanged will handle the filter reset
        // If we are already there, we might need to force reset if filter was somehow changed?
        // But Filter is bound to selection. So just navigating/selecting is enough.
        // If we are already selected, SelectionChanged won't fire.
        // So we explicitly reset here just in case.
        if (NavigationView.SelectedItem == LibraryNavItem)
        {
             ViewModel.LibraryViewModel.SelectGroupById(string.Empty);
        }
    }

    private void RefreshLibraryNavigationGroups()
    {
        if (LibraryNavItem == null)
        {
            return;
        }

        LibraryNavItem.MenuItems.Clear();
        var groups = ViewModel.LibraryViewModel.Groups.Where(group => !string.IsNullOrEmpty(group.Id)).ToList();

        foreach (var group in groups)
        {
            var item = new NavigationViewItem
            {
                Content = group.Name,
                TargetPageType = typeof(LibraryPage),
                Tag = group.Id  // Tag 用于在 SelectionChanged 中识别分组
            };
            LibraryNavItem.MenuItems.Add(item);
        }

        LibraryNavItem.IsExpanded = groups.Count > 0;
        NavigationView.RegisterNestedMenuItems(LibraryNavItem.MenuItems);
    }

    private static NavigationViewItem? FindAncestorNavigationItem(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is NavigationViewItem navigationItem)
            {
                return navigationItem;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }
}

/// <summary>
/// 简单的页面服务实现
/// </summary>
public class PageService : IPageService
{
    private readonly MainWindow _mainWindow;
    private readonly MainWindowViewModel _viewModel;
    private ProjectDetailPage? _projectDetailPage;
    private LibraryPage? _libraryPage;

    public PageService(MainWindow mainWindow, MainWindowViewModel viewModel)
    {
        _mainWindow = mainWindow;
        _viewModel = viewModel;
    }

    /// <summary>
    /// 设置项目详情页面实例
    /// </summary>
    public void SetProjectDetailPage(ProjectDetailPage? page)
    {
        _projectDetailPage = page;
    }

    public T? GetPage<T>() where T : class
    {
        return GetPage(typeof(T)) as T;
    }

    public FrameworkElement? GetPage(Type pageType)
    {
        if (pageType == typeof(LibraryPage))
        {
            // 缓存 LibraryPage 实例，确保分组筛选功能正常工作
            if (_libraryPage == null)
            {
                _libraryPage = new LibraryPage(_viewModel.LibraryViewModel);
            }
            return _libraryPage;
        }
        else if (pageType == typeof(AllSkillsPage))
        {
             // AllSkillsPage is just a wrapper for LibraryPage visual
             // BUT we need it to be a distinct instance or type for NavigationView to work.
             // We can return a NEW instance of AllSkillsPage
             return new AllSkillsPage(_viewModel.LibraryViewModel);
        }
        else if (pageType == typeof(ScanPage))
        {
            return new ScanPage(_viewModel.ScanViewModel);
        }
        else if (pageType == typeof(SettingsPage))
        {
            return new SettingsPage();
        }
        else if (pageType == typeof(ProjectListPage))
        {
            var listViewModel = _viewModel.CreateProjectListViewModel(project =>
            {
                _mainWindow.NavigateToProjectDetail(project);
            });
            return new ProjectListPage(listViewModel, project =>
            {
                _mainWindow.NavigateToProjectDetail(project);
            });
        }
        else if (pageType == typeof(DownloadSkillsPage))
        {
            return new DownloadSkillsPage(_viewModel.DownloadSkillsViewModel);
        }
        else if (pageType == typeof(CleanupPage))
        {
            return new CleanupPage(_viewModel.CleanupViewModel);
        }
        else if (pageType == typeof(AutomationPage))
        {
            return new AutomationPage(_viewModel.AutomationViewModel);
        }
        else if (pageType == typeof(ProjectDetailPage))
        {
            // 返回预先创建的详情页面实例
            return _projectDetailPage;
        }

        return null;
    }
}
