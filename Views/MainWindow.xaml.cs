using SkillManager.Models;
using SkillManager.ViewModels;
using System.Windows;
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

        // 默认导航到Library页面
        Loaded += (s, e) => NavigationView.Navigate(typeof(LibraryPage));
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
}

/// <summary>
/// 简单的页面服务实现
/// </summary>
public class PageService : IPageService
{
    private readonly MainWindow _mainWindow;
    private readonly MainWindowViewModel _viewModel;
    private ProjectDetailPage? _projectDetailPage;

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
            return new LibraryPage(_viewModel.LibraryViewModel);
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

