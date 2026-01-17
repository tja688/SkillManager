using SkillManager.ViewModels;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace SkillManager.Views;

/// <summary>
/// LibraryPage.xaml 的交互逻辑
/// </summary>
public partial class LibraryPage : Page
{
    public LibraryViewModel ViewModel { get; }

    public LibraryPage(LibraryViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }
}
