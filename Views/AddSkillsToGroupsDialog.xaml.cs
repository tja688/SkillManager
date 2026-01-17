using CommunityToolkit.Mvvm.ComponentModel;
using SkillManager.Models;
using SkillManager.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Wpf.Ui.Controls;

namespace SkillManager.Views;

public partial class AddSkillsToGroupsDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<SkillGroup> _groups = new();

    [ObservableProperty]
    private bool _hasNoGroups = true;

    [ObservableProperty]
    private int _selectedSkillCount;

    partial void OnGroupsChanged(ObservableCollection<SkillGroup> value)
    {
        HasNoGroups = value.Count == 0;
    }

    public void UpdateHasNoGroups()
    {
        HasNoGroups = Groups.Count == 0;
    }
}

public partial class AddSkillsToGroupsDialog : FluentWindow
{
    private readonly GroupService _groupService;
    private readonly AddSkillsToGroupsDialogViewModel _viewModel;

    public AddSkillsToGroupsDialog(GroupService groupService, int selectedSkillCount)
    {
        _groupService = groupService;
        _viewModel = new AddSkillsToGroupsDialogViewModel
        {
            SelectedSkillCount = selectedSkillCount
        };
        DataContext = _viewModel;

        InitializeComponent();
        Loaded += async (_, _) => await LoadGroupsAsync();
    }

    public IReadOnlyList<string> SelectedGroupIds =>
        _viewModel.Groups.Where(group => group.IsSelected).Select(group => group.Id).ToList();

    private async Task LoadGroupsAsync()
    {
        var groups = await _groupService.GetAllGroupsAsync();
        _viewModel.Groups.Clear();
        foreach (var group in groups)
        {
            group.IsSelected = false;
            _viewModel.Groups.Add(group);
        }
        _viewModel.UpdateHasNoGroups();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Groups.All(group => !group.IsSelected))
        {
            System.Windows.MessageBox.Show(
                "请选择至少一个分组。",
                "提示",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }
}
