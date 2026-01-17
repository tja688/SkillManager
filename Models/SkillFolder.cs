using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;

namespace SkillManager.Models;

/// <summary>
/// 技能文件夹模型
/// </summary>
public partial class SkillFolder : ObservableObject
{
    /// <summary>
    /// 文件夹名称
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// 完整路径
    /// </summary>
    [ObservableProperty]
    private string _fullPath = string.Empty;

    /// <summary>
    /// SKILL.md 文件内容摘要
    /// </summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    [ObservableProperty]
    private DateTime _createdTime;

    /// <summary>
    /// 是否已在Library中
    /// </summary>
    [ObservableProperty]
    private bool _isInLibrary;

    /// <summary>
    /// 技能标题（SKILL.md 中的一级标题）
    /// </summary>
    [ObservableProperty]
    private string _skillTitle = string.Empty;

    /// <summary>
    /// 使用场景说明（When to Use / 能做什么）
    /// </summary>
    [ObservableProperty]
    private string _whenToUse = string.Empty;

    /// <summary>
    /// 是否展开详情
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    /// SKILL.md文件的完整路径
    /// </summary>
    public string SkillMdPath => Path.Combine(FullPath, "SKILL.md");
}
