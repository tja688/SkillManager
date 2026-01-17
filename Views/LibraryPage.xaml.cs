using SkillManager.ViewModels;
using System.Windows.Controls;

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
        DataContext = ViewModel;
        InitializeComponent();

        // 每次加载时刷新列表
        Loaded += (s, e) => ViewModel.RefreshSkills();
    }
}
