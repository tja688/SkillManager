using SkillManager.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace SkillManager.Views;

/// <summary>
/// SelectSkillDialog.xaml 的交互逻辑
/// </summary>
public partial class SelectSkillDialog : FluentWindow
{
    private readonly Action<string> _onSearch;

    public SkillFolder? SelectedSkill { get; private set; }

    public SelectSkillDialog(ObservableCollection<SkillFolder> skills, Action<string> onSearch)
    {
        _onSearch = onSearch;
        InitializeComponent();
        SkillsList.ItemsSource = skills;

        UpdateEmptyState(skills.Count == 0);
    }

    private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _onSearch(SearchTextBox.Text);
        
        if (SkillsList.ItemsSource is ObservableCollection<SkillFolder> skills)
        {
            UpdateEmptyState(skills.Count == 0);
        }
    }

    private void UpdateEmptyState(bool isEmpty)
    {
        EmptyText.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SkillItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is SkillFolder skill)
        {
            SelectedSkill = skill;
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
