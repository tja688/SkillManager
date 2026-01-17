using SkillManager.Models;
using System.IO;

namespace SkillManager.Services;

/// <summary>
/// Library 管理服务
/// </summary>
public class LibraryService
{
    private readonly string _libraryPath;

    public LibraryService(string libraryPath)
    {
        _libraryPath = libraryPath;
        EnsureLibraryExists();
    }

    /// <summary>
    /// 确保library目录存在
    /// </summary>
    private void EnsureLibraryExists()
    {
        if (!Directory.Exists(_libraryPath))
        {
            Directory.CreateDirectory(_libraryPath);
        }
    }

    /// <summary>
    /// 获取library路径
    /// </summary>
    public string LibraryPath => _libraryPath;

    /// <summary>
    /// 获取library中所有技能
    /// </summary>
    public List<SkillFolder> GetAllSkills()
    {
        var skills = new List<SkillFolder>();

        if (!Directory.Exists(_libraryPath)) return skills;

        foreach (var dir in Directory.GetDirectories(_libraryPath))
        {
            var skillMdPath = Path.Combine(dir, "SKILL.md");
            if (File.Exists(skillMdPath))
            {
                skills.Add(new SkillFolder
                {
                    Name = Path.GetFileName(dir),
                    FullPath = dir,
                    Description = GetSkillDescription(skillMdPath),
                    CreatedTime = Directory.GetCreationTime(dir),
                    IsInLibrary = true
                });
            }
        }

        return skills.OrderBy(s => s.Name).ToList();
    }

    /// <summary>
    /// 导入技能到library
    /// </summary>
    public async Task<bool> ImportSkillAsync(SkillFolder skill, IProgress<string>? progress = null)
    {
        try
        {
            var destPath = Path.Combine(_libraryPath, skill.Name);

            if (Directory.Exists(destPath))
            {
                progress?.Report($"技能 {skill.Name} 已存在于Library中");
                return false;
            }

            progress?.Report($"正在导入: {skill.Name}");

            await Task.Run(() =>
            {
                CopyDirectory(skill.FullPath, destPath);
            });

            progress?.Report($"导入成功: {skill.Name}");
            return true;
        }
        catch (Exception ex)
        {
            progress?.Report($"导入失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 批量导入技能
    /// </summary>
    public async Task<int> ImportSkillsAsync(IEnumerable<SkillFolder> skills, IProgress<string>? progress = null)
    {
        var successCount = 0;

        foreach (var skill in skills)
        {
            if (await ImportSkillAsync(skill, progress))
            {
                successCount++;
            }
        }

        return successCount;
    }

    /// <summary>
    /// 从library删除技能
    /// </summary>
    public async Task<bool> DeleteSkillAsync(SkillFolder skill, IProgress<string>? progress = null)
    {
        try
        {
            var skillPath = Path.Combine(_libraryPath, skill.Name);

            if (!Directory.Exists(skillPath))
            {
                progress?.Report($"技能 {skill.Name} 不存在");
                return false;
            }

            progress?.Report($"正在删除: {skill.Name}");

            await Task.Run(() =>
            {
                Directory.Delete(skillPath, true);
            });

            progress?.Report($"删除成功: {skill.Name}");
            return true;
        }
        catch (Exception ex)
        {
            progress?.Report($"删除失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 打开技能文件夹
    /// </summary>
    public void OpenSkillFolder(SkillFolder skill)
    {
        var path = skill.IsInLibrary ? Path.Combine(_libraryPath, skill.Name) : skill.FullPath;

        if (Directory.Exists(path))
        {
            System.Diagnostics.Process.Start("explorer.exe", path);
        }
    }

    /// <summary>
    /// 递归复制目录
    /// </summary>
    private void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir);
        }
    }

    /// <summary>
    /// 获取技能描述
    /// </summary>
    private string GetSkillDescription(string skillMdPath)
    {
        try
        {
            var lines = File.ReadAllLines(skillMdPath);
            var inFrontMatter = false;

            foreach (var line in lines)
            {
                if (line.Trim() == "---")
                {
                    inFrontMatter = !inFrontMatter;
                    continue;
                }

                if (inFrontMatter && line.TrimStart().StartsWith("description:"))
                {
                    return line.Substring(line.IndexOf(':') + 1).Trim();
                }
            }

            // 返回第一行非空内容
            return lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("---"))?.Trim() ?? "";
        }
        catch
        {
            return "";
        }
    }
}
