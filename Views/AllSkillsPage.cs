using SkillManager.ViewModels;

namespace SkillManager.Views;

/// <summary>
/// 专门用于显示"所有技能"的页面（继承自LibraryPage）
/// 通过使用不同的类型，解决NavigationView无法区分父级和子级选中状态的问题
/// </summary>
public class AllSkillsPage : LibraryPage
{
    public AllSkillsPage(LibraryViewModel viewModel) : base(viewModel)
    {
    }
}
