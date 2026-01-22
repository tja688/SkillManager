namespace SkillManager.Models;

public class SkillManagerSettings
{
    public List<string> ProtectedPaths { get; set; } = new();
    public List<string> AutomationPaths { get; set; } = new();


    /// <summary>
    /// 自动化轮询监控间隔（秒），默认10秒
    /// </summary>
    public int AutomationPollingIntervalSeconds { get; set; } = 10;
}
