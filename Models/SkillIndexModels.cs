using System.Text.Json.Serialization;

namespace SkillManager.Models;

/// <summary>
/// 技能索引项
/// </summary>
public class SkillIndexItem
{
    /// <summary>
    /// 技能ID (路径Hash)
    /// </summary>
    public string SkillId { get; set; } = string.Empty;

    /// <summary>
    /// 文件夹名
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 绝对路径
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// SKILL.md 路径
    /// </summary>
    public string SkillMdPath { get; set; } = string.Empty;

    /// <summary>
    /// SKILL.md 最后修改时间 (UTC)
    /// </summary>
    public DateTime LastWriteTimeUtc { get; set; }

    /// <summary>
    /// 描述 (缓存)
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 技能标题（SKILL.md 中的一级标题）
    /// </summary>
    public string SkillTitle { get; set; } = string.Empty;

    /// <summary>
    /// 使用场景说明（When to Use / 能做什么）
    /// </summary>
    public string WhenToUse { get; set; } = string.Empty;
}

/// <summary>
/// 项目技能索引
/// </summary>
public class ProjectSkillIndex
{
    public string ProjectId { get; set; } = string.Empty;

    public DateTime LastScanTime { get; set; }

    /// <summary>
    /// Key: ZoneFullPath, Value: List of Skills
    /// </summary>
    public Dictionary<string, List<SkillIndexItem>> Zones { get; set; } = new();
}

/// <summary>
/// 技能库索引
/// </summary>
public class LibrarySkillIndex
{
    public DateTime LastScanTime { get; set; }
    public List<SkillIndexItem> Skills { get; set; } = new();
}

