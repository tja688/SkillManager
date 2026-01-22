using System.Windows;
using System.Windows.Controls;
using SkillManager.ViewModels;

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

    private async void PollingIntervalBox_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.NumberBox numberBox && numberBox.Value.HasValue && ViewModel != null)
        {
            await ViewModel.UpdatePollingIntervalCommand.ExecuteAsync((int)numberBox.Value.Value);
        }
    }
}
