using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace SkillManager.Models;

/// <summary>
/// 项目模型
/// </summary>
public partial class Project : ObservableObject
{
    /// <summary>
    /// 项目唯一ID
    /// </summary>
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    /// <summary>
    /// 项目名称
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// 项目路径
    /// </summary>
    [ObservableProperty]
    private string _path = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    [ObservableProperty]
    private DateTime _createdTime = DateTime.Now;

    /// <summary>
    /// 技能区列表（不序列化，运行时动态加载）
    /// </summary>
    [JsonIgnore]
    [ObservableProperty]
    private ObservableCollection<SkillZone> _skillZones = new();

    /// <summary>
    /// 总技能数
    /// </summary>
    [JsonIgnore]
    public int TotalSkillCount => SkillZones?.Sum(z => z.Skills?.Count ?? 0) ?? 0;

    /// <summary>
    /// 刷新技能数量通知
    /// </summary>
    public void RefreshSkillCount()
    {
        OnPropertyChanged(nameof(TotalSkillCount));
    }
}

/// <summary>
/// 技能区模型（如 .claude, .codex, .agent 等）
/// </summary>
public partial class SkillZone : ObservableObject
{
    /// <summary>
    /// 技能区名称（如 .claude）
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// 技能区完整路径
    /// </summary>
    [ObservableProperty]
    private string _fullPath = string.Empty;

    /// <summary>
    /// 内部skills文件夹路径
    /// </summary>
    public string SkillsFolderPath => System.IO.Path.Combine(FullPath, "skills");

    /// <summary>
    /// 技能列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<SkillFolder> _skills = new();

    /// <summary>
    /// 是否展开
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded = true;
}

/// <summary>
/// 扫描预览结果（用于添加项目时预览）
/// </summary>
public class SkillZonePreview
{
    /// <summary>
    /// 技能区名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 技能区路径
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 内部技能数量
    /// </summary>
    public int SkillCount { get; set; }
}
