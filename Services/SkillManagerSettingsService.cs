using System.IO;
using System.Text.Json;
using SkillManager.Models;

namespace SkillManager.Services;

public class SkillManagerSettingsService
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public SkillManagerSettingsService(string dataDirectory)
    {
        _filePath = Path.Combine(dataDirectory, "skill_manager_settings.json");
    }

    public async Task<List<string>> LoadProtectedPathsAsync()
    {
        var settings = await LoadAsync();
        return settings.ProtectedPaths ?? new List<string>();
    }

    public async Task<List<string>> LoadAutomationPathsAsync()
    {
        var settings = await LoadAsync();
        return settings.AutomationPaths ?? new List<string>();
    }

    public async Task SaveProtectedPathsAsync(IEnumerable<string> paths)
    {
        var settings = await LoadAsync();
        settings.ProtectedPaths = PathUtilities.NormalizePaths(paths);
        await SaveAsync(settings);
    }

    public async Task SaveAutomationPathsAsync(IEnumerable<string> paths)
    {
        var settings = await LoadAsync();
        settings.AutomationPaths = PathUtilities.NormalizePaths(paths);
        await SaveAsync(settings);
    }

    public async Task<int> LoadPollingIntervalAsync()
    {
        var settings = await LoadAsync();
        return settings.AutomationPollingIntervalSeconds;
    }

    public async Task SavePollingIntervalAsync(int intervalSeconds)
    {
        var settings = await LoadAsync();
        settings.AutomationPollingIntervalSeconds = Math.Max(5, Math.Min(300, intervalSeconds)); // 限制在5-300秒
        await SaveAsync(settings);
    }

    private async Task<SkillManagerSettings> LoadAsync()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new SkillManagerSettings();
            }

            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<SkillManagerSettings>(json) ?? new SkillManagerSettings();
        }
        catch
        {
            return new SkillManagerSettings();
        }
    }

    private async Task SaveAsync(SkillManagerSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, _options);
            await File.WriteAllTextAsync(_filePath, json);
        }
        catch
        {
        }
    }
}
