namespace SkillManager.Models;

/// <summary>
/// 扫描结果模型
/// </summary>
public class ScanResult
{
    /// <summary>
    /// 扫描到的技能文件夹列表
    /// </summary>
    public List<SkillFolder> FoundSkills { get; set; } = new();

    /// <summary>
    /// 扫描用时
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }

    /// <summary>
    /// 扫描的目录数
    /// </summary>
    public int ScannedDirectories { get; set; }

    /// <summary>
    /// 跳过的重复名称技能数
    /// </summary>
    public int SkippedDuplicateCount { get; set; }

    /// <summary>
    /// 跳过的已存在于库中的技能数
    /// </summary>
    public int SkippedLibraryCount { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}
