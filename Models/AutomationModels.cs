namespace SkillManager.Models;

public class AutomationLogItem
{
    public string SkillName { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsSuccess { get; set; }
}

public class AutomationRunResult
{
    public int FoundSkills { get; set; }
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ScannedPaths { get; set; }
    public List<AutomationLogItem> Logs { get; set; } = new();
}
