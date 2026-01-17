using SkillManager.ViewModels;
using System.Windows.Controls;

namespace SkillManager.Views;

/// <summary>
/// ScanPage.xaml 的交互逻辑
/// </summary>
public partial class ScanPage : Page
{
    public ScanViewModel ViewModel { get; }

    public ScanPage(ScanViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;
        InitializeComponent();
    }
}
