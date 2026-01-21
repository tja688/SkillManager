using SkillManager.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SkillManager.Views;

public partial class CleanupPage : Page
{
    public CleanupViewModel ViewModel { get; }

    public CleanupPage(CleanupViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadProtectedPathsAsync();
    }
}
