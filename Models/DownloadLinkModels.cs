using CommunityToolkit.Mvvm.ComponentModel;

namespace SkillManager.Models;

public partial class DownloadLinkItem : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private DateTime _createdTime;

    [ObservableProperty]
    private bool _isSelected;
}

public class DownloadLinkRecord
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
}

public class DownloadLinkIndex
{
    public DateTime LastUpdatedTime { get; set; } = DateTime.UtcNow;
    public List<DownloadLinkRecord> Links { get; set; } = new();
}
