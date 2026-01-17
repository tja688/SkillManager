using SkillManager.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SkillManager.Services;

/// <summary>
/// 项目管理服务
/// </summary>
public class ProjectService
{
    private readonly string _projectsFilePath;
    private readonly LibraryService _libraryService;
    private List<Project> _projects = new();

    public ProjectService(string dataDirectory, LibraryService libraryService)
    {
        _projectsFilePath = Path.Combine(dataDirectory, "projects.json");
        _libraryService = libraryService;
        LoadProjects();
    }

    /// <summary>
    /// 获取所有项目
    /// </summary>
    public List<Project> GetAllProjects() => _projects.ToList();

    /// <summary>
    /// 根据ID获取项目
    /// </summary>
    public Project? GetProjectById(string id) => _projects.FirstOrDefault(p => p.Id == id);

    /// <summary>
    /// 扫描路径下的技能区预览
    /// </summary>
    public async Task<List<SkillZonePreview>> ScanSkillZonesPreviewAsync(string rootPath)
    {
        var previews = new List<SkillZonePreview>();

        await Task.Run(() =>
        {
            if (!Directory.Exists(rootPath)) return;

            // 搜索以"."开头的文件夹
            var dotFolders = Directory.GetDirectories(rootPath)
                .Where(d => Path.GetFileName(d).StartsWith("."))
                .ToList();

            foreach (var dotFolder in dotFolders)
            {
                var skillsPath = Path.Combine(dotFolder, "skills");
                if (Directory.Exists(skillsPath))
                {
                    var skillCount = CountSkillsInFolder(skillsPath);
                    previews.Add(new SkillZonePreview
                    {
                        Name = Path.GetFileName(dotFolder),
                        Path = dotFolder,
                        SkillCount = skillCount
                    });
                }
            }

            // 递归搜索子目录中以"."开头的文件夹
            try
            {
                foreach (var subDir in Directory.GetDirectories(rootPath))
                {
                    var folderName = Path.GetFileName(subDir);
                    // 跳过特殊目录
                    if (folderName.StartsWith(".") || 
                        folderName == "node_modules" || 
                        folderName == "bin" || 
                        folderName == "obj")
                        continue;

                    SearchSkillZonesRecursive(subDir, previews, 3); // 限制递归深度
                }
            }
            catch { }
        });

        return previews;
    }

    private void SearchSkillZonesRecursive(string path, List<SkillZonePreview> previews, int depth)
    {
        if (depth <= 0) return;

        try
        {
            var dotFolders = Directory.GetDirectories(path)
                .Where(d => Path.GetFileName(d).StartsWith("."))
                .ToList();

            foreach (var dotFolder in dotFolders)
            {
                var skillsPath = Path.Combine(dotFolder, "skills");
                if (Directory.Exists(skillsPath))
                {
                    // 检查是否已添加
                    if (!previews.Any(p => p.Path == dotFolder))
                    {
                        var skillCount = CountSkillsInFolder(skillsPath);
                        previews.Add(new SkillZonePreview
                        {
                            Name = Path.GetFileName(dotFolder),
                            Path = dotFolder,
                            SkillCount = skillCount
                        });
                    }
                }
            }

            foreach (var subDir in Directory.GetDirectories(path))
            {
                var folderName = Path.GetFileName(subDir);
                if (folderName.StartsWith(".") || 
                    folderName == "node_modules" || 
                    folderName == "bin" || 
                    folderName == "obj")
                    continue;

                SearchSkillZonesRecursive(subDir, previews, depth - 1);
            }
        }
        catch { }
    }

    private int CountSkillsInFolder(string skillsPath)
    {
        try
        {
            return Directory.GetDirectories(skillsPath)
                .Count(d => File.Exists(Path.Combine(d, "SKILL.md")));
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 创建项目
    /// </summary>
    public async Task<Project> CreateProjectAsync(string name, string path)
    {
        var project = new Project
        {
            Name = name,
            Path = path,
            CreatedTime = DateTime.Now
        };

        _projects.Add(project);
        await SaveProjectsAsync();

        // 加载技能区
        await LoadSkillZonesAsync(project);

        return project;
    }

    /// <summary>
    /// 加载项目的技能区
    /// </summary>
    public async Task LoadSkillZonesAsync(Project project)
    {
        project.SkillZones.Clear();

        await Task.Run(() =>
        {
            if (!Directory.Exists(project.Path)) return;

            var previews = new List<SkillZonePreview>();
            
            // 搜索所有技能区
            var dotFolders = Directory.GetDirectories(project.Path)
                .Where(d => Path.GetFileName(d).StartsWith("."))
                .ToList();

            foreach (var dotFolder in dotFolders)
            {
                var skillsPath = Path.Combine(dotFolder, "skills");
                if (Directory.Exists(skillsPath))
                {
                    var zone = new SkillZone
                    {
                        Name = Path.GetFileName(dotFolder),
                        FullPath = dotFolder
                    };

                    // 加载技能
                    LoadSkillsInZone(zone);

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        project.SkillZones.Add(zone);
                    });
                }
            }

            // 递归搜索
            SearchAndAddSkillZones(project.Path, project, 3);
        });

        project.RefreshSkillCount();
    }

    private void SearchAndAddSkillZones(string path, Project project, int depth)
    {
        if (depth <= 0) return;

        try
        {
            foreach (var subDir in Directory.GetDirectories(path))
            {
                var folderName = Path.GetFileName(subDir);
                
                if (folderName.StartsWith("."))
                {
                    var skillsPath = Path.Combine(subDir, "skills");
                    if (Directory.Exists(skillsPath))
                    {
                        // 检查是否已添加
                        var exists = false;
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            exists = project.SkillZones.Any(z => z.FullPath == subDir);
                        });

                        if (!exists)
                        {
                            var zone = new SkillZone
                            {
                                Name = folderName,
                                FullPath = subDir
                            };
                            LoadSkillsInZone(zone);

                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                project.SkillZones.Add(zone);
                            });
                        }
                    }
                    continue;
                }

                if (folderName == "node_modules" || folderName == "bin" || folderName == "obj")
                    continue;

                SearchAndAddSkillZones(subDir, project, depth - 1);
            }
        }
        catch { }
    }

    private void LoadSkillsInZone(SkillZone zone)
    {
        try
        {
            var skillsPath = zone.SkillsFolderPath;
            if (!Directory.Exists(skillsPath)) return;

            foreach (var skillDir in Directory.GetDirectories(skillsPath))
            {
                var skillMdPath = Path.Combine(skillDir, "SKILL.md");
                if (File.Exists(skillMdPath))
                {
                    var skill = new SkillFolder
                    {
                        Name = Path.GetFileName(skillDir),
                        FullPath = skillDir,
                        Description = GetSkillDescription(skillMdPath),
                        CreatedTime = Directory.GetCreationTime(skillDir)
                    };

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        zone.Skills.Add(skill);
                    });
                }
            }
        }
        catch { }
    }

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

            return lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("---"))?.Trim() ?? "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// 添加技能区
    /// </summary>
    public async Task<SkillZone?> AddSkillZoneAsync(Project project, string zoneName)
    {
        // 确保以"."开头
        if (!zoneName.StartsWith("."))
            zoneName = "." + zoneName;

        var zonePath = Path.Combine(project.Path, zoneName);
        var skillsPath = Path.Combine(zonePath, "skills");

        try
        {
            await Task.Run(() =>
            {
                Directory.CreateDirectory(skillsPath);
            });

            var zone = new SkillZone
            {
                Name = zoneName,
                FullPath = zonePath
            };

            project.SkillZones.Add(zone);
            project.RefreshSkillCount();

            return zone;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 删除技能区（真删）
    /// </summary>
    public async Task<bool> DeleteSkillZoneAsync(Project project, SkillZone zone)
    {
        try
        {
            await Task.Run(() =>
            {
                if (Directory.Exists(zone.FullPath))
                {
                    Directory.Delete(zone.FullPath, true);
                }
            });

            project.SkillZones.Remove(zone);
            project.RefreshSkillCount();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 从仓库添加技能到技能区
    /// </summary>
    public async Task<bool> AddSkillToZoneAsync(SkillZone zone, SkillFolder librarySkill)
    {
        try
        {
            var destPath = Path.Combine(zone.SkillsFolderPath, librarySkill.Name);

            if (Directory.Exists(destPath))
                return false;

            await Task.Run(() =>
            {
                CopyDirectory(librarySkill.FullPath, destPath);
            });

            var skill = new SkillFolder
            {
                Name = librarySkill.Name,
                FullPath = destPath,
                Description = librarySkill.Description,
                CreatedTime = DateTime.Now
            };

            zone.Skills.Add(skill);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 从仓库添加技能到项目的所有技能区
    /// </summary>
    public async Task<int> AddSkillToProjectAsync(Project project, SkillFolder librarySkill)
    {
        var successCount = 0;

        foreach (var zone in project.SkillZones)
        {
            if (await AddSkillToZoneAsync(zone, librarySkill))
                successCount++;
        }

        project.RefreshSkillCount();
        return successCount;
    }

    /// <summary>
    /// 删除技能区中的技能
    /// </summary>
    public async Task<bool> DeleteSkillFromZoneAsync(SkillZone zone, SkillFolder skill)
    {
        try
        {
            await Task.Run(() =>
            {
                if (Directory.Exists(skill.FullPath))
                {
                    Directory.Delete(skill.FullPath, true);
                }
            });

            zone.Skills.Remove(skill);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 从项目的所有技能区删除同名技能
    /// </summary>
    public async Task<int> DeleteSkillFromProjectAsync(Project project, string skillName)
    {
        var successCount = 0;

        foreach (var zone in project.SkillZones)
        {
            var skill = zone.Skills.FirstOrDefault(s => s.Name == skillName);
            if (skill != null)
            {
                if (await DeleteSkillFromZoneAsync(zone, skill))
                    successCount++;
            }
        }

        project.RefreshSkillCount();
        return successCount;
    }

    /// <summary>
    /// 删除项目（假删，只删除软件内的记录）
    /// </summary>
    public async Task<bool> DeleteProjectAsync(Project project)
    {
        _projects.Remove(project);
        await SaveProjectsAsync();
        return true;
    }

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

    private void LoadProjects()
    {
        try
        {
            if (File.Exists(_projectsFilePath))
            {
                var json = File.ReadAllText(_projectsFilePath);
                _projects = JsonSerializer.Deserialize<List<Project>>(json) ?? new List<Project>();
            }
        }
        catch
        {
            _projects = new List<Project>();
        }
    }

    private async Task SaveProjectsAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_projects, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_projectsFilePath, json);
        }
        catch { }
    }

    /// <summary>
    /// 获取Library服务
    /// </summary>
    public LibraryService LibraryService => _libraryService;

    /// <summary>
    /// 保存展开状态
    /// </summary>
    public async Task SaveExpandStatesAsync(Project project)
    {
        try
        {
            var statesPath = Path.Combine(Path.GetDirectoryName(_projectsFilePath)!, $"expand_states_{project.Id}.json");
            var states = project.SkillZones.ToDictionary(z => z.FullPath, z => z.IsExpanded);
            var json = JsonSerializer.Serialize(states, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(statesPath, json);
        }
        catch { }
    }

    /// <summary>
    /// 加载展开状态
    /// </summary>
    public async Task LoadExpandStatesAsync(Project project)
    {
        try
        {
            var statesPath = Path.Combine(Path.GetDirectoryName(_projectsFilePath)!, $"expand_states_{project.Id}.json");
            if (File.Exists(statesPath))
            {
                var json = await File.ReadAllTextAsync(statesPath);
                var states = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
                if (states != null)
                {
                    foreach (var zone in project.SkillZones)
                    {
                        if (states.TryGetValue(zone.FullPath, out var isExpanded))
                        {
                            zone.IsExpanded = isExpanded;
                        }
                    }
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// 获取索引文件路径
    /// </summary>
    private string GetIndexFilePath(string projectId)
    {
        return Path.Combine(Path.GetDirectoryName(_projectsFilePath)!, $"skill_index_{projectId}.json");
    }

    /// <summary>
    /// 计算Path Hash
    /// </summary>
    private string ComputeSkillId(string path)
    {
        using var md5 = MD5.Create();
        var bytes = Encoding.UTF8.GetBytes(path.ToLowerInvariant());
        var hash = md5.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// 加载项目索引
    /// </summary>
    private async Task<ProjectSkillIndex?> LoadProjectIndexAsync(string projectId)
    {
        try
        {
            var path = GetIndexFilePath(projectId);
            if (!File.Exists(path)) return null;

            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<ProjectSkillIndex>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 保存项目索引
    /// </summary>
    private async Task SaveProjectIndexAsync(ProjectSkillIndex index)
    {
        try
        {
            var path = GetIndexFilePath(index.ProjectId);
            var json = JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = false });
            await File.WriteAllTextAsync(path, json);
        }
        catch { }
    }
}
