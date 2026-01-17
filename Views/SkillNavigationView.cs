using System.Collections;
using System.Windows;
using Wpf.Ui.Controls;

namespace SkillManager.Views;

public class SkillNavigationView : NavigationView
{
    static SkillNavigationView()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SkillNavigationView),
            new FrameworkPropertyMetadata(typeof(NavigationView)));
    }

    public void RegisterNestedMenuItems(IEnumerable items)
    {
        UpdateMenuItemsTemplate(items);
        AddItemsToDictionaries(items);
    }
}
