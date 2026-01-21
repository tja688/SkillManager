namespace SkillManager.Models;

public class SkillManagerSettings
{
    public List<string> ProtectedPaths { get; set; } = new();
    public List<string> AutomationPaths { get; set; } = new();
}
