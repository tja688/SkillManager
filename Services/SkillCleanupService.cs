using SkillManager.Models;
using System.IO;

namespace SkillManager.Services;

public class SkillCleanupService
{
    public async Task<bool> DeleteSkillAsync(SkillFolder skill, IReadOnlyCollection<string> normalizedProtectedPaths, IProgress<string>? progress = null)
    {
        if (skill == null) return false;

        var path = skill.FullPath;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            progress?.Report("路径无效或不存在");
            return false;
        }

        if (PathUtilities.IsPathUnder(path, normalizedProtectedPaths))
        {
            progress?.Report("保护区内的技能已跳过");
            return false;
        }

        var skillMdPath = Path.Combine(path, "SKILL.md");
        if (!File.Exists(skillMdPath))
        {
            progress?.Report("缺少 SKILL.md，已跳过");
            return false;
        }

        try
        {
            progress?.Report($"正在删除: {skill.Name}");
            await Task.Run(() => Directory.Delete(path, true));
            progress?.Report($"已删除: {skill.Name}");
            return true;
        }
        catch (Exception ex)
        {
            progress?.Report($"删除失败: {ex.Message}");
            return false;
        }
    }
}
