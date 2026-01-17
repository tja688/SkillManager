using SkillManager.Models;
using SkillManager.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

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

        SkillsScrollViewer.AddHandler(
            PreviewMouseWheelEvent,
            new MouseWheelEventHandler(SkillsScrollViewer_PreviewMouseWheel),
            true);
    }

    private void SkillCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (IsFromInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (sender is FrameworkElement element && element.DataContext is SkillFolder skill)
        {
            ViewModel.HandleCardClick(skill);
            e.Handled = true;
        }
    }

    private static bool IsFromInteractiveElement(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is ButtonBase || source is TextBoxBase)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void SkillsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            if (scrollViewer.ScrollableHeight <= 0)
            {
                return;
            }

            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }
}
