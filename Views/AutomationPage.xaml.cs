using SkillManager.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SkillManager.Views;

public partial class AutomationPage : Page
{
    public AutomationViewModel ViewModel { get; }

    public AutomationPage(AutomationViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadWatchPathsAsync();
    }
}
