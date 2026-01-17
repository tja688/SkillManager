using SkillManager.Models;
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
    private Project? _currentDetailProject;

    public MainWindow()
    {
        ViewModel = new MainWindowViewModel();
        DataContext = ViewModel;

        InitializeComponent();

        // 设置导航服务
        _pageService = new PageService(this, ViewModel);
        NavigationView.SetPageService(_pageService);

        ViewModel.LibraryViewModel.GroupsRefreshed += RefreshLibraryNavigationGroups;

        // 默认导航到Library页面
        Loaded += (s, e) =>
        {
            RefreshLibraryNavigationGroups();
            NavigationView.Navigate(typeof(LibraryPage));
        };
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

    private void NavigationView_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not NavigationView navigationView)
        {
            return;
        }

        if (navigationView.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        if (item.TargetPageType == typeof(LibraryPage))
        {
            var groupId = item.Tag as string;
            // 应用分组筛选（无论是否重新导航，都会使用同一个 LibraryPage 实例）
            ViewModel.LibraryViewModel.SelectGroupById(groupId ?? string.Empty);
        }
    }

    private void LibraryNavItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var clickedItem = FindAncestorNavigationItem(e.OriginalSource as DependencyObject);
        if (clickedItem != null && !ReferenceEquals(clickedItem, sender))
        {
            return;
        }

        ViewModel.LibraryViewModel.SelectGroupById(string.Empty);
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
        else if (pageType == typeof(ProjectDetailPage))
        {
            // 返回预先创建的详情页面实例
            return _projectDetailPage;
        }

        return null;
    }
}

