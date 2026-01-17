using CommunityToolkit.Mvvm.ComponentModel;

namespace SkillManager.Models;

/// <summary>
/// 技能分组模型
/// </summary>
public partial class SkillGroup : ObservableObject
{
    /// <summary>
    /// 分组ID (GUID)
    /// </summary>
    [ObservableProperty]
    private string _id = string.Empty;

    /// <summary>
    /// 分组名称
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    [ObservableProperty]
    private DateTime _createdTime;

    /// <summary>
    /// 分组包含的技能名称列表
    /// </summary>
    [ObservableProperty]
    private List<string> _skillNames = new();

    /// <summary>
    /// 是否被选中（用于UI绑定）
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// 技能分组索引（用于JSON持久化）
/// </summary>
public class SkillGroupIndex
{
    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime LastUpdateTime { get; set; }

    /// <summary>
    /// 所有分组
    /// </summary>
    public List<SkillGroupItem> Groups { get; set; } = new();
}

/// <summary>
/// 分组索引项
/// </summary>
public class SkillGroupItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
    public List<string> SkillNames { get; set; } = new();
}
