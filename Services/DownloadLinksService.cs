using SkillManager.Models;
using System.IO;
using System.Text.Json;

namespace SkillManager.Services;

public class DownloadLinksService
{
    private readonly string _filePath;

    public DownloadLinksService(string dataDirectory)
    {
        _filePath = Path.Combine(dataDirectory, "download_links.json");
    }

    public async Task<List<DownloadLinkRecord>> LoadLinksAsync()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new List<DownloadLinkRecord>();
            }

            var json = await File.ReadAllTextAsync(_filePath);
            var index = JsonSerializer.Deserialize<DownloadLinkIndex>(json);
            return index?.Links ?? new List<DownloadLinkRecord>();
        }
        catch
        {
            return new List<DownloadLinkRecord>();
        }
    }

    public async Task SaveLinksAsync(IEnumerable<DownloadLinkRecord> links)
    {
        try
        {
            var index = new DownloadLinkIndex
            {
                LastUpdatedTime = DateTime.UtcNow,
                Links = links.ToList()
            };

            var json = JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
        }
        catch
        {
        }
    }
}
