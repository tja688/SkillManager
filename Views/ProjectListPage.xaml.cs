using SkillManager.Models;
using SkillManager.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SkillManager.Views;

/// <summary>
/// ProjectListPage.xaml 的交互逻辑
/// </summary>
public partial class ProjectListPage : Page
{
    public ProjectListViewModel ViewModel { get; }
    private readonly Action<Project> _navigateToDetail;

    public ProjectListPage(ProjectListViewModel viewModel, Action<Project> navigateToDetail)
    {
        ViewModel = viewModel;
        _navigateToDetail = navigateToDetail;
        DataContext = ViewModel;
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshProjectsAsync();
    }

    private void AddProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddProjectDialog(ViewModel.ProjectService);
        dialog.Owner = Window.GetWindow(this);
        
        if (dialog.ShowDialog() == true && dialog.CreatedProject != null)
        {
            ViewModel.Projects.Add(dialog.CreatedProject);
            ViewModel.RefreshProjectsCommand.Execute(null);
        }
    }

    private void ProjectCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is Project project)
        {
            _navigateToDetail(project);
        }
    }
}
