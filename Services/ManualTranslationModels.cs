using System;
using System.Collections.Generic;

namespace SkillManager.Services;

public sealed class ManualTranslationFile
{
    public int Version { get; set; } = 1;
    public DateTime GeneratedAtUtc { get; set; }
    public string LibraryPath { get; set; } = string.Empty;
    public List<ManualTranslationSkill> Skills { get; set; } = new();
}

public sealed class ManualTranslationSkill
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public ManualTranslationField Description { get; set; } = new();
    public ManualTranslationField WhenToUse { get; set; } = new();
}

public sealed class ManualTranslationField
{
    public string Source { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
}
