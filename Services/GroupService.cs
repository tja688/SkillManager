using SkillManager.Models;
using System.IO;
using System.Text.Json;

namespace SkillManager.Services;

/// <summary>
/// 技能分组管理服务
/// </summary>
public class GroupService
{
    private readonly string _libraryPath;
    private readonly string _groupIndexPath;
    private SkillGroupIndex? _cachedIndex;

    public GroupService(string libraryPath)
    {
        _libraryPath = libraryPath;
        _groupIndexPath = Path.Combine(libraryPath, ".groups_index.json");
    }

    #region 索引管理

    /// <summary>
    /// 加载分组索引
    /// </summary>
    private async Task<SkillGroupIndex> LoadIndexAsync()
    {
        try
        {
            if (!File.Exists(_groupIndexPath))
            {
                return new SkillGroupIndex { LastUpdateTime = DateTime.UtcNow };
            }

            var json = await File.ReadAllTextAsync(_groupIndexPath);
            return JsonSerializer.Deserialize<SkillGroupIndex>(json) ?? new SkillGroupIndex();
        }
        catch
        {
            return new SkillGroupIndex { LastUpdateTime = DateTime.UtcNow };
        }
    }

    /// <summary>
    /// 保存分组索引
    /// </summary>
    private async Task SaveIndexAsync(SkillGroupIndex index)
    {
        try
        {
            index.LastUpdateTime = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_groupIndexPath, json);
            _cachedIndex = index;
        }
        catch { }
    }

    #endregion

    #region 公共 API

    /// <summary>
    /// 获取所有分组
    /// </summary>
    public async Task<List<SkillGroup>> GetAllGroupsAsync()
    {
        var index = _cachedIndex ?? await LoadIndexAsync();
        _cachedIndex = index;

        return index.Groups.Select(g => new SkillGroup
        {
            Id = g.Id,
            Name = g.Name,
            CreatedTime = g.CreatedTime,
            SkillNames = g.SkillNames.ToList()
        }).OrderBy(g => g.Name).ToList();
    }

    /// <summary>
    /// 创建新分组
    /// </summary>
    public async Task<SkillGroup> CreateGroupAsync(string name)
    {
        var index = _cachedIndex ?? await LoadIndexAsync();
        
        // 检查是否已存在同名分组
        if (index.Groups.Any(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"分组 '{name}' 已存在");
        }

        var newGroup = new SkillGroupItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            CreatedTime = DateTime.UtcNow,
            SkillNames = new List<string>()
        };

        index.Groups.Add(newGroup);
        await SaveIndexAsync(index);

        return new SkillGroup
        {
            Id = newGroup.Id,
            Name = newGroup.Name,
            CreatedTime = newGroup.CreatedTime,
            SkillNames = new List<string>()
        };
    }

    /// <summary>
    /// 删除分组
    /// </summary>
    public async Task<bool> DeleteGroupAsync(string groupId)
    {
        var index = _cachedIndex ?? await LoadIndexAsync();
        var removed = index.Groups.RemoveAll(g => g.Id == groupId);
        
        if (removed > 0)
        {
            await SaveIndexAsync(index);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 获取技能所属的分组
    /// </summary>
    public async Task<List<SkillGroup>> GetGroupsForSkillAsync(string skillName)
    {
        var index = _cachedIndex ?? await LoadIndexAsync();
        _cachedIndex = index;

        return index.Groups
            .Where(g => g.SkillNames.Contains(skillName))
            .Select(g => new SkillGroup
            {
                Id = g.Id,
                Name = g.Name,
                CreatedTime = g.CreatedTime,
                SkillNames = g.SkillNames.ToList()
            })
            .OrderBy(g => g.Name)
            .ToList();
    }

    /// <summary>
    /// 将技能添加到分组
    /// </summary>
    public async Task<bool> AddSkillToGroupAsync(string skillName, string groupId)
    {
        var index = _cachedIndex ?? await LoadIndexAsync();
        var group = index.Groups.FirstOrDefault(g => g.Id == groupId);
        
        if (group == null) return false;

        if (!group.SkillNames.Contains(skillName))
        {
            group.SkillNames.Add(skillName);
            await SaveIndexAsync(index);
        }

        return true;
    }

    /// <summary>
    /// 将技能从分组移除
    /// </summary>
    public async Task<bool> RemoveSkillFromGroupAsync(string skillName, string groupId)
    {
        var index = _cachedIndex ?? await LoadIndexAsync();
        var group = index.Groups.FirstOrDefault(g => g.Id == groupId);
        
        if (group == null) return false;

        if (group.SkillNames.Remove(skillName))
        {
            await SaveIndexAsync(index);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 获取分组内的所有技能名称
    /// </summary>
    public async Task<HashSet<string>> GetSkillsInGroupAsync(string groupId)
    {
        var index = _cachedIndex ?? await LoadIndexAsync();
        _cachedIndex = index;

        var group = index.Groups.FirstOrDefault(g => g.Id == groupId);
        return group != null ? new HashSet<string>(group.SkillNames) : new HashSet<string>();
    }

    /// <summary>
    /// 当技能被删除时，从所有分组中移除
    /// </summary>
    public async Task RemoveSkillFromAllGroupsAsync(string skillName)
    {
        var index = _cachedIndex ?? await LoadIndexAsync();
        var modified = false;

        foreach (var group in index.Groups)
        {
            if (group.SkillNames.Remove(skillName))
            {
                modified = true;
            }
        }

        if (modified)
        {
            await SaveIndexAsync(index);
        }
    }

    #endregion
}
