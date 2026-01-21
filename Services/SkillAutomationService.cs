using SkillManager.Models;
using System.IO;

namespace SkillManager.Services;

public class SkillAutomationService
{
    private readonly SkillScannerService _scannerService;
    private readonly LibraryService _libraryService;

    public SkillAutomationService(SkillScannerService scannerService, LibraryService libraryService)
    {
        _scannerService = scannerService;
        _libraryService = libraryService;
    }

    public async Task<AutomationRunResult> RunAutoImportAsync(IEnumerable<string> watchPaths, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var result = new AutomationRunResult();
        var normalizedPaths = PathUtilities.NormalizePaths(watchPaths);
        var filteredPaths = FilterNestedPaths(normalizedPaths);

        foreach (var path in filteredPaths)
        {
            if (cancellationToken.IsCancellationRequested) break;

            if (!Directory.Exists(path))
            {
                result.Logs.Add(new AutomationLogItem
                {
                    SkillName = GetPathLabel(path),
                    Result = "跳过",
                    Message = "路径不存在",
                    SourcePath = path,
                    IsSuccess = false,
                    Timestamp = DateTime.Now
                });
                continue;
            }

            progress?.Report($"扫描: {path}");
            var scanResult = await _scannerService.ScanAsync(path, progress, cancellationToken);
            result.ScannedPaths++;

            if (!scanResult.IsSuccess)
            {
                result.Logs.Add(new AutomationLogItem
                {
                    SkillName = GetPathLabel(path),
                    Result = "失败",
                    Message = scanResult.ErrorMessage ?? "扫描失败",
                    SourcePath = path,
                    IsSuccess = false,
                    Timestamp = DateTime.Now
                });
                continue;
            }

            foreach (var skill in scanResult.FoundSkills.OrderBy(s => s.Name))
            {
                if (cancellationToken.IsCancellationRequested) break;

                result.FoundSkills++;
                var destPath = Path.Combine(_libraryService.LibraryPath, skill.Name);
                if (Directory.Exists(destPath))
                {
                    result.SkippedCount++;
                    result.Logs.Add(new AutomationLogItem
                    {
                        SkillName = skill.Name,
                        SourcePath = skill.FullPath,
                        Result = "跳过",
                        Message = "库中已存在同名技能",
                        IsSuccess = false,
                        Timestamp = DateTime.Now
                    });
                    continue;
                }

                var importSuccess = await _libraryService.ImportSkillAsync(skill, progress);
                if (!importSuccess)
                {
                    result.SkippedCount++;
                    result.Logs.Add(new AutomationLogItem
                    {
                        SkillName = skill.Name,
                        SourcePath = skill.FullPath,
                        Result = "跳过",
                        Message = "导入失败",
                        IsSuccess = false,
                        Timestamp = DateTime.Now
                    });
                    continue;
                }

                var deleteSuccess = TryDeleteSource(skill.FullPath);
                result.ImportedCount++;
                result.Logs.Add(new AutomationLogItem
                {
                    SkillName = skill.Name,
                    SourcePath = skill.FullPath,
                    Result = "已导入",
                    Message = deleteSuccess ? "已剪切到库" : "已导入，原路径未删除",
                    IsSuccess = true,
                    Timestamp = DateTime.Now
                });
            }
        }

        return result;
    }

    private static string GetPathLabel(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "未知路径";
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    private bool TryDeleteSource(string sourcePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath)) return false;

            var normalizedLibrary = PathUtilities.NormalizePath(_libraryService.LibraryPath);
            if (PathUtilities.IsPathUnder(sourcePath, new[] { normalizedLibrary }))
            {
                return false;
            }

            if (!Directory.Exists(sourcePath)) return false;
            Directory.Delete(sourcePath, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static List<string> FilterNestedPaths(List<string> normalizedPaths)
    {
        var filtered = new List<string>();

        foreach (var path in normalizedPaths.OrderBy(p => p.Length))
        {
            if (!PathUtilities.IsPathUnder(path, filtered))
            {
                filtered.Add(path);
            }
        }

        return filtered;
    }
}
