using System;
using System.Collections.Generic;

namespace SkillManager.Services;

public static class TranslationFields
{
    public const string WhenToUse = "WhenToUse";
    public const string Description = "Description";
}

public enum TranslationStatus
{
    Ready,
    Failed
}

public sealed class TranslationRecord
{
    public string SkillId { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string TargetLang { get; set; } = "zh-CN";
    public string EngineId { get; set; } = string.Empty;
    public string EngineVersion { get; set; } = string.Empty;
    public string SourceHash { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public TranslationStatus Status { get; set; } = TranslationStatus.Ready;
    public string Error { get; set; } = string.Empty;
}

public sealed class TranslationCacheFile
{
    public int SchemaVersion { get; set; } = 1;
    public Dictionary<string, TranslationRecord> Records { get; set; } = new(StringComparer.Ordinal);
}

public readonly record struct TranslationKey(
    string SkillId,
    string Field,
    string TargetLang,
    string EngineId,
    string EngineVersion,
    string SourceHash)
{
    public string CacheKey => $"{SkillId}|{Field}|{TargetLang}|{EngineId}|{EngineVersion}|{SourceHash}";
}

public sealed class TranslationProgressInfo
{
    public int Total { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public string CurrentSkillName { get; set; } = string.Empty;
    public string CurrentField { get; set; } = string.Empty;
}

public sealed class TranslationQueuedEventArgs : EventArgs
{
    public TranslationQueuedEventArgs(string skillId, string field)
    {
        SkillId = skillId;
        Field = field;
    }

    public string SkillId { get; }
    public string Field { get; }
}

public sealed class TranslationCompletedEventArgs : EventArgs
{
    public TranslationCompletedEventArgs(string skillId, string field, bool success, string? translatedText, string? error)
    {
        SkillId = skillId;
        Field = field;
        Success = success;
        TranslatedText = translatedText;
        Error = error;
    }

    public string SkillId { get; }
    public string Field { get; }
    public bool Success { get; }
    public string? TranslatedText { get; }
    public string? Error { get; }
}

public sealed class TranslationSettings
{
    public string EngineId { get; set; } = "onnx-marian";
    public string EngineVersion { get; set; } = "unknown";
    public string SourceLang { get; set; } = "en";
    public string TargetLang { get; set; } = "zh-CN";
    public int MaxConcurrency { get; set; } = 1;
    public int MaxLength { get; set; } = 96;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);
    public bool EnableTranslation { get; set; } = true;
}

public sealed class TranslationPair
{
    public string WhenToUse { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
