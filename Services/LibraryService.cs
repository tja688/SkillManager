using SkillManager.Models;
using System.IO;
using System.Text.Json;

namespace SkillManager.Services;

/// <summary>
/// Library 管理服务 - 带索引缓存
/// </summary>
public class LibraryService
{
    private readonly string _libraryPath;
    private readonly string _indexFilePath;
    
    /// <summary>
    /// 内存缓存的索引
    /// </summary>
    private LibrarySkillIndex? _cachedIndex;
    
    /// <summary>
    /// 上次磁盘扫描时间
    /// </summary>
    private DateTime _lastDiskScan = DateTime.MinValue;

    public LibraryService(string libraryPath)
    {
        _libraryPath = libraryPath;
        _indexFilePath = Path.Combine(libraryPath, ".library_index.json");
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

    #region 索引管理

    /// <summary>
    /// 加载索引（异步）
    /// </summary>
    private async Task<LibrarySkillIndex?> LoadIndexAsync()
    {
        try
        {
            if (!File.Exists(_indexFilePath)) return null;

            var json = await File.ReadAllTextAsync(_indexFilePath);
            return JsonSerializer.Deserialize<LibrarySkillIndex>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 保存索引（异步）
    /// </summary>
    private async Task SaveIndexAsync(LibrarySkillIndex index)
    {
        try
        {
            var json = JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = false });
            await File.WriteAllTextAsync(_indexFilePath, json);
        }
        catch { }
    }

    /// <summary>
    /// 从磁盘扫描并构建索引
    /// </summary>
    private async Task<LibrarySkillIndex> ScanAndBuildIndexAsync()
    {
        var index = new LibrarySkillIndex
        {
            LastScanTime = DateTime.UtcNow,
            Skills = new List<SkillIndexItem>()
        };

        await Task.Run(() =>
        {
            if (!Directory.Exists(_libraryPath)) return;

            foreach (var dir in Directory.GetDirectories(_libraryPath))
            {
                var skillMdPath = Path.Combine(dir, "SKILL.md");
                if (File.Exists(skillMdPath))
                {
                    var lastWriteTime = File.GetLastWriteTimeUtc(skillMdPath);
                    var skillInfo = ParseSkillInfo(skillMdPath);
                    index.Skills.Add(new SkillIndexItem
                    {
                        SkillId = Path.GetFileName(dir),
                        Name = Path.GetFileName(dir),
                        Path = dir,
                        SkillMdPath = skillMdPath,
                        LastWriteTimeUtc = lastWriteTime,
                        Description = skillInfo.Description,
                        SkillTitle = skillInfo.SkillTitle,
                        WhenToUse = skillInfo.WhenToUse
                    });
                }
            }

            index.Skills = index.Skills.OrderBy(s => s.Name).ToList();
        });

        _cachedIndex = index;
        _lastDiskScan = DateTime.Now;
        await SaveIndexAsync(index);

        return index;
    }

    /// <summary>
    /// 增量刷新索引（检测变化）
    /// </summary>
    private async Task<LibrarySkillIndex> RefreshIndexAsync(LibrarySkillIndex existingIndex)
    {
        var newIndex = new LibrarySkillIndex
        {
            LastScanTime = DateTime.UtcNow,
            Skills = new List<SkillIndexItem>()
        };

        await Task.Run(() =>
        {
            if (!Directory.Exists(_libraryPath)) return;

            var existingLookup = existingIndex.Skills.ToDictionary(s => s.Name, s => s);

            foreach (var dir in Directory.GetDirectories(_libraryPath))
            {
                var skillName = Path.GetFileName(dir);
                var skillMdPath = Path.Combine(dir, "SKILL.md");

                if (File.Exists(skillMdPath))
                {
                    var lastWriteTime = File.GetLastWriteTimeUtc(skillMdPath);

                    // 检查缓存
                    if (existingLookup.TryGetValue(skillName, out var cached) &&
                        cached.LastWriteTimeUtc == lastWriteTime)
                    {
                        // 未修改，复用缓存
                        newIndex.Skills.Add(cached);
                    }
                    else
                    {
                        // 新增或修改，重新解析
                        var skillInfo = ParseSkillInfo(skillMdPath);
                        newIndex.Skills.Add(new SkillIndexItem
                        {
                            SkillId = skillName,
                            Name = skillName,
                            Path = dir,
                            SkillMdPath = skillMdPath,
                            LastWriteTimeUtc = lastWriteTime,
                            Description = skillInfo.Description,
                            SkillTitle = skillInfo.SkillTitle,
                            WhenToUse = skillInfo.WhenToUse
                        });
                    }
                }
            }

            newIndex.Skills = newIndex.Skills.OrderBy(s => s.Name).ToList();
        });

        _cachedIndex = newIndex;
        _lastDiskScan = DateTime.Now;
        await SaveIndexAsync(newIndex);

        return newIndex;
    }

    #endregion

    #region 公共 API

    /// <summary>
    /// 异步获取所有技能（带缓存）
    /// </summary>
    /// <param name="forceRefresh">强制刷新索引</param>
    public async Task<List<SkillFolder>> GetAllSkillsAsync(bool forceRefresh = false)
    {
        LibrarySkillIndex? index;

        if (forceRefresh)
        {
            // 强制全量刷新
            index = await ScanAndBuildIndexAsync();
        }
        else if (_cachedIndex != null)
        {
            // 使用内存缓存
            index = _cachedIndex;
            
            // 后台静默增量刷新（不阻塞）
            _ = Task.Run(async () => await RefreshIndexAsync(index));
        }
        else
        {
            // 尝试加载磁盘索引
            index = await LoadIndexAsync();

            if (index != null)
            {
                _cachedIndex = index;
                // 后台增量刷新
                _ = Task.Run(async () => await RefreshIndexAsync(index));
            }
            else
            {
                // 无索引，全量扫描
                index = await ScanAndBuildIndexAsync();
            }
        }

        return index.Skills.Select(s => new SkillFolder
        {
            Name = s.Name,
            FullPath = s.Path,
            Description = s.Description,
            SkillTitle = s.SkillTitle,
            WhenToUse = s.WhenToUse,
            CreatedTime = s.LastWriteTimeUtc.ToLocalTime(),
            IsInLibrary = true
        }).ToList();
    }

    /// <summary>
    /// 同步获取所有技能（兼容旧代码，内部调用异步方法）
    /// </summary>
    public List<SkillFolder> GetAllSkills()
    {
        // 如果有内存缓存直接返回
        if (_cachedIndex != null)
        {
            return _cachedIndex.Skills.Select(s => new SkillFolder
            {
                Name = s.Name,
                FullPath = s.Path,
                Description = s.Description,
                SkillTitle = s.SkillTitle,
                WhenToUse = s.WhenToUse,
                CreatedTime = s.LastWriteTimeUtc.ToLocalTime(),
                IsInLibrary = true
            }).ToList();
        }

        // 否则同步加载索引
        var index = LoadIndexAsync().GetAwaiter().GetResult();
        if (index != null)
        {
            _cachedIndex = index;
            return index.Skills.Select(s => new SkillFolder
            {
                Name = s.Name,
                FullPath = s.Path,
                Description = s.Description,
                SkillTitle = s.SkillTitle,
                WhenToUse = s.WhenToUse,
                CreatedTime = s.LastWriteTimeUtc.ToLocalTime(),
                IsInLibrary = true
            }).ToList();
        }

        // 无索引，同步扫描（首次使用会慢一次）
        var skills = new List<SkillFolder>();

        if (!Directory.Exists(_libraryPath)) return skills;

        foreach (var dir in Directory.GetDirectories(_libraryPath))
        {
            var skillMdPath = Path.Combine(dir, "SKILL.md");
            if (File.Exists(skillMdPath))
            {
                var skillInfo = ParseSkillInfo(skillMdPath);
                skills.Add(new SkillFolder
                {
                    Name = Path.GetFileName(dir),
                    FullPath = dir,
                    Description = skillInfo.Description,
                    SkillTitle = skillInfo.SkillTitle,
                    WhenToUse = skillInfo.WhenToUse,
                    CreatedTime = Directory.GetCreationTime(dir),
                    IsInLibrary = true
                });
            }
        }

        return skills.OrderBy(s => s.Name).ToList();
    }

    #endregion

    #region 导入/删除操作

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

            // 更新索引
            await UpdateIndexAfterImportAsync(skill.Name, destPath);

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
    /// 导入后更新索引
    /// </summary>
    private async Task UpdateIndexAfterImportAsync(string skillName, string skillPath)
    {
        var skillMdPath = Path.Combine(skillPath, "SKILL.md");
        if (!File.Exists(skillMdPath)) return;

        var index = _cachedIndex ?? await LoadIndexAsync() ?? new LibrarySkillIndex();

        // 移除旧的（如果存在）
        index.Skills.RemoveAll(s => s.Name == skillName);

        // 添加新的
        index.Skills.Add(new SkillIndexItem
        {
            SkillId = skillName,
            Name = skillName,
            Path = skillPath,
            SkillMdPath = skillMdPath,
            LastWriteTimeUtc = File.GetLastWriteTimeUtc(skillMdPath),
            Description = GetSkillDescription(skillMdPath)
        });

        index.Skills = index.Skills.OrderBy(s => s.Name).ToList();
        index.LastScanTime = DateTime.UtcNow;

        _cachedIndex = index;
        await SaveIndexAsync(index);
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

            // 更新索引
            await UpdateIndexAfterDeleteAsync(skill.Name);

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
    /// 删除后更新索引
    /// </summary>
    private async Task UpdateIndexAfterDeleteAsync(string skillName)
    {
        var index = _cachedIndex ?? await LoadIndexAsync();
        if (index == null) return;

        index.Skills.RemoveAll(s => s.Name == skillName);
        index.LastScanTime = DateTime.UtcNow;

        _cachedIndex = index;
        await SaveIndexAsync(index);
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

    #endregion

    #region 辅助方法

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
    /// 解析技能信息（描述、标题、使用场景）
    /// </summary>
    private (string Description, string SkillTitle, string WhenToUse) ParseSkillInfo(string skillMdPath)
    {
        try
        {
            var lines = File.ReadAllLines(skillMdPath);
            var description = string.Empty;
            var skillTitle = string.Empty;
            var whenToUse = string.Empty;

            var inFrontMatter = false;
            var currentSection = string.Empty;
            var sectionContent = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmedLine = line.Trim();

                // 处理 frontmatter
                if (trimmedLine == "---")
                {
                    if (inFrontMatter)
                    {
                        inFrontMatter = false;
                        continue;
                    }
                    else if (i == 0)
                    {
                        inFrontMatter = true;
                        continue;
                    }
                }

                if (inFrontMatter)
                {
                    if (trimmedLine.StartsWith("description:"))
                    {
                        description = trimmedLine.Substring(trimmedLine.IndexOf(':') + 1).Trim();
                    }
                    continue;
                }

                // 解析一级标题（# Title）
                if (string.IsNullOrEmpty(skillTitle) && trimmedLine.StartsWith("# ") && !trimmedLine.StartsWith("##"))
                {
                    skillTitle = trimmedLine.Substring(2).Trim();
                    continue;
                }

                // 检测二级标题开始新段落
                if (trimmedLine.StartsWith("## "))
                {
                    // 保存之前的段落内容
                    if (!string.IsNullOrEmpty(currentSection) && sectionContent.Count > 0)
                    {
                        var content = string.Join("\n", sectionContent).Trim();
                        if (IsWhenToUseSection(currentSection))
                        {
                            whenToUse = content;
                        }
                    }

                    currentSection = trimmedLine.Substring(3).Trim();
                    sectionContent.Clear();
                    continue;
                }

                // 收集当前段落内容（限制长度）
                if (!string.IsNullOrEmpty(currentSection) && sectionContent.Count < 15)
                {
                    if (!string.IsNullOrWhiteSpace(trimmedLine) && !trimmedLine.StartsWith("```"))
                    {
                        sectionContent.Add(trimmedLine);
                    }
                }

                // 如果已经找到 whenToUse，可以提前退出
                if (!string.IsNullOrEmpty(whenToUse))
                {
                    break;
                }
            }

            // 处理最后一个段落
            if (string.IsNullOrEmpty(whenToUse) && !string.IsNullOrEmpty(currentSection) && sectionContent.Count > 0)
            {
                if (IsWhenToUseSection(currentSection))
                {
                    whenToUse = string.Join("\n", sectionContent).Trim();
                }
            }

            // 如果没有找到具体使用场景，使用 description
            if (string.IsNullOrEmpty(whenToUse) && !string.IsNullOrEmpty(description))
            {
                whenToUse = description;
            }

            return (description, skillTitle, whenToUse);
        }
        catch
        {
            return (string.Empty, string.Empty, string.Empty);
        }
    }

    /// <summary>
    /// 判断是否为使用场景相关的段落标题
    /// </summary>
    private bool IsWhenToUseSection(string sectionTitle)
    {
        var lowerTitle = sectionTitle.ToLowerInvariant();
        return lowerTitle.Contains("when to use") ||
               lowerTitle.Contains("overview") ||
               lowerTitle.Contains("about") ||
               lowerTitle.Contains("使用场景") ||
               lowerTitle.Contains("能做什么") ||
               lowerTitle.Contains("功能");
    }

    /// <summary>
    /// 获取技能描述（兼容旧代码）
    /// </summary>
    private string GetSkillDescription(string skillMdPath)
    {
        return ParseSkillInfo(skillMdPath).Description;
    }

    #endregion
}
