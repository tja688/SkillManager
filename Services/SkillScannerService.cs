using SkillManager.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SkillManager.Services;

/// <summary>
/// 扫描上下文，用于在并行扫描中共享状态
/// </summary>
internal class ScanContext
{
    public ConcurrentBag<SkillFolder> FoundSkills { get; } = new();
    public HashSet<string> ExistingSkills { get; set; } = new();
    public IReadOnlyList<string> ExcludedPaths { get; set; } = Array.Empty<string>();
    /// <summary>
    /// 用于在扫描过程中跟踪已发现的文件夹名称，防止重复添加（线程安全）
    /// </summary>
    public ConcurrentDictionary<string, byte> DiscoveredNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// 已扫描过的完整路径，用于跳过重复扫描（线程安全）
    /// </summary>
    public ConcurrentDictionary<string, byte> ScannedPaths { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int ScannedCount;
    public int SkippedDuplicateCount;
    public int SkippedLibraryCount;
    public IProgress<string>? Progress { get; set; }
    public CancellationToken CancellationToken { get; set; }
}

/// <summary>
/// 技能扫描服务 - 高性能并行扫描
/// </summary>
public class SkillScannerService
{
    private readonly string _libraryPath;

    // 排除的系统目录
    private static readonly HashSet<string> SystemExcludedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "$Recycle.Bin", "Windows", "Program Files", "Program Files (x86)",
        "ProgramData", "Recovery", "System Volume Information",
        "node_modules", ".git", ".svn", ".hg", "bin", "obj",
        "__pycache__", ".vs", ".idea", ".vscode", "packages"
    };

    public SkillScannerService(string libraryPath)
    {
        _libraryPath = libraryPath;
    }

    /// <summary>
    /// 扫描指定路径下的技能文件夹
    /// </summary>
    public async Task<ScanResult> ScanAsync(string rootPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default, IEnumerable<string>? excludedPaths = null)
    {
        var result = new ScanResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var context = new ScanContext
            {
                ExistingSkills = GetExistingLibrarySkillNames(),
                Progress = progress,
                CancellationToken = cancellationToken,
                ExcludedPaths = PathUtilities.NormalizePaths(excludedPaths ?? Array.Empty<string>())
            };

            progress?.Report($"开始扫描: {rootPath}");

            await Task.Run(() => ScanDirectory(rootPath, context), cancellationToken);

            stopwatch.Stop();

            result.FoundSkills = context.FoundSkills.ToList();
            result.ElapsedTime = stopwatch.Elapsed;
            result.ScannedDirectories = context.ScannedCount;
            result.SkippedDuplicateCount = context.SkippedDuplicateCount;
            result.SkippedLibraryCount = context.SkippedLibraryCount;
            result.IsSuccess = true;

            progress?.Report($"扫描完成! 找到 {result.FoundSkills.Count} 个技能，跳过 {context.SkippedDuplicateCount} 个重复，{context.SkippedLibraryCount} 个已存在");
        }
        catch (OperationCanceledException)
        {
            result.IsSuccess = false;
            result.ErrorMessage = "扫描已取消";
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// 全局扫描所有驱动器
    /// </summary>
    public async Task<ScanResult> ScanGlobalAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default, IEnumerable<string>? excludedPaths = null)
    {
        var result = new ScanResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var context = new ScanContext
            {
                ExistingSkills = GetExistingLibrarySkillNames(),
                Progress = progress,
                CancellationToken = cancellationToken,
                ExcludedPaths = PathUtilities.NormalizePaths(excludedPaths ?? Array.Empty<string>())
            };

            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .ToList();

            progress?.Report($"开始全局扫描 {drives.Count} 个驱动器...");

            // 并行扫描所有驱动器
            var tasks = drives.Select(drive =>
                Task.Run(() => ScanDirectory(drive.RootDirectory.FullName, context), cancellationToken));

            await Task.WhenAll(tasks);

            stopwatch.Stop();

            result.FoundSkills = context.FoundSkills.ToList();
            result.ElapsedTime = stopwatch.Elapsed;
            result.ScannedDirectories = context.ScannedCount;
            result.SkippedDuplicateCount = context.SkippedDuplicateCount;
            result.SkippedLibraryCount = context.SkippedLibraryCount;
            result.IsSuccess = true;

            progress?.Report($"全局扫描完成! 共找到 {result.FoundSkills.Count} 个技能，跳过 {context.SkippedDuplicateCount} 个重复，{context.SkippedLibraryCount} 个已存在");
        }
        catch (OperationCanceledException)
        {
            result.IsSuccess = false;
            result.ErrorMessage = "扫描已取消";
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private void ScanDirectory(string path, ScanContext context)
    {
        if (context.CancellationToken.IsCancellationRequested) return;

        try
        {
            // 检查是否已扫描过此路径（避免符号链接等导致的重复）
            if (!context.ScannedPaths.TryAdd(path.ToLowerInvariant(), 0))
            {
                return;
            }

            if (PathUtilities.IsPathUnder(path, context.ExcludedPaths))
            {
                return;
            }

            // 检查是否是排除目录
            var dirName = Path.GetFileName(path);
            if (SystemExcludedNames.Contains(dirName)) return;

            // 跳过 library 目录本身
            if (path.Equals(_libraryPath, StringComparison.OrdinalIgnoreCase)) return;

            Interlocked.Increment(ref context.ScannedCount);

            // 检查是否包含 SKILL.md
            var skillMdPath = Path.Combine(path, "SKILL.md");
            if (File.Exists(skillMdPath))
            {
                var folderName = Path.GetFileName(path);

                // 检查是否已在 library 中
                if (context.ExistingSkills.Contains(folderName))
                {
                    Interlocked.Increment(ref context.SkippedLibraryCount);
                    context.Progress?.Report($"跳过（库中已存在）: {folderName}");
                    // 找到SKILL.md后不再深入扫描该目录的子目录
                    return;
                }

                // 使用 TryAdd 确保相同名称的文件夹只添加一次
                if (context.DiscoveredNames.TryAdd(folderName, 0))
                {
                    var skill = new SkillFolder
                    {
                        Name = folderName,
                        FullPath = path,
                        Description = GetSkillDescription(skillMdPath),
                        CreatedTime = Directory.GetCreationTime(path),
                        IsInLibrary = false
                    };

                    context.FoundSkills.Add(skill);
                    context.Progress?.Report($"发现技能: {folderName}");
                }
                else
                {
                    Interlocked.Increment(ref context.SkippedDuplicateCount);
                    context.Progress?.Report($"跳过（重复名称）: {folderName}");
                }

                // 找到SKILL.md后不再深入扫描该目录的子目录
                return;
            }

            // 获取子目录
            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(path);
            }
            catch
            {
                return;
            }

            // 使用Parallel加速
            Parallel.ForEach(subDirs, new ParallelOptions
            {
                CancellationToken = context.CancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, subDir =>
            {
                ScanDirectory(subDir, context);
            });
        }
        catch (UnauthorizedAccessException) { }
        catch (PathTooLongException) { }
        catch (IOException) { }
    }

    /// <summary>
    /// 获取技能描述（从SKILL.md的frontmatter中提取）
    /// </summary>
    private string GetSkillDescription(string skillMdPath)
    {
        try
        {
            var content = File.ReadAllText(skillMdPath, Encoding.UTF8);

            // 尝试从YAML frontmatter提取description
            var match = Regex.Match(content, @"^---\s*\n.*?description:\s*(.+?)\n.*?---", RegexOptions.Singleline);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            // 如果没有frontmatter，取前100个字符
            var firstLine = content.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("---"));
            return firstLine?.Trim().Substring(0, Math.Min(100, firstLine.Trim().Length)) ?? "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// 获取library中已有的技能名称
    /// </summary>
    public HashSet<string> GetExistingLibrarySkillNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(_libraryPath))
        {
            Directory.CreateDirectory(_libraryPath);
            return names;
        }

        foreach (var dir in Directory.GetDirectories(_libraryPath))
        {
            var skillMdPath = Path.Combine(dir, "SKILL.md");
            if (File.Exists(skillMdPath))
            {
                names.Add(Path.GetFileName(dir));
            }
        }

        return names;
    }
}
