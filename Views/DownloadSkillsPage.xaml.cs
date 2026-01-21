using SkillManager.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SkillManager.Views;

public partial class DownloadSkillsPage : Page
{
    public DownloadSkillsViewModel ViewModel { get; }

    public DownloadSkillsPage(DownloadSkillsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadLinksAsync();
    }
}
