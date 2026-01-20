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
    /// 使用场景中文翻译（缓存）
    /// </summary>
    [ObservableProperty]
    private string _whenToUseZh = string.Empty;

    /// <summary>
    /// 描述中文翻译（缓存）
    /// </summary>
    [ObservableProperty]
    private string _descriptionZh = string.Empty;

    /// <summary>
    /// 是否展开详情
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    /// 所属分组显示文本
    /// </summary>
    [ObservableProperty]
    private string _groupNamesDisplay = "未分组";

    /// <summary>
    /// 是否被多选选中
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// 翻译进行中标记
    /// </summary>
    [ObservableProperty]
    private bool _isTranslationPending;

    /// <summary>
    /// 翻译状态提示
    /// </summary>
    [ObservableProperty]
    private string _translationStatusMessage = string.Empty;

    /// <summary>
    /// SKILL.md文件的完整路径
    /// </summary>
    public string SkillMdPath => Path.Combine(FullPath, "SKILL.md");

    public string SkillId => NormalizeSkillId(FullPath);

    public string WhenToUseDisplay => string.IsNullOrWhiteSpace(WhenToUseZh) ? WhenToUse : WhenToUseZh;

    public string DescriptionDisplay => string.IsNullOrWhiteSpace(DescriptionZh) ? Description : DescriptionZh;

    partial void OnWhenToUseChanged(string value) => OnPropertyChanged(nameof(WhenToUseDisplay));

    partial void OnWhenToUseZhChanged(string value) => OnPropertyChanged(nameof(WhenToUseDisplay));

    partial void OnDescriptionChanged(string value) => OnPropertyChanged(nameof(DescriptionDisplay));

    partial void OnDescriptionZhChanged(string value) => OnPropertyChanged(nameof(DescriptionDisplay));

    partial void OnFullPathChanged(string value) => OnPropertyChanged(nameof(SkillId));

    private static string NormalizeSkillId(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        return path.Trim()
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/')
            .ToLowerInvariant();
    }
}
