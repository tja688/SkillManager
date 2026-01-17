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
    /// SKILL.md文件的完整路径
    /// </summary>
    public string SkillMdPath => Path.Combine(FullPath, "SKILL.md");
}
