using System.IO;
using System.Text.Json;

namespace SkillManager.Services;

public static class TranslationSettingsLoader
{
    public static (TranslationSettings Settings, TranslationModelConfig ModelConfig) Load(string modelDirectory, string libraryPath)
    {
        var modelConfig = TranslationModelConfig.Load(modelDirectory);
        var settings = new TranslationSettings
        {
            EngineVersion = modelConfig.EngineVersion,
            MaxLength = modelConfig.MaxLength
        };

        var metaPath = Path.Combine(libraryPath, ".translation_meta.json");
        if (File.Exists(metaPath))
        {
            try
            {
                var json = File.ReadAllText(metaPath);
                var meta = JsonSerializer.Deserialize<TranslationMeta>(json);
                if (meta != null)
                {
                    settings.EnableTranslation = !meta.DisableTranslation;
                    if (meta.MaxConcurrency.HasValue)
                    {
                        settings.MaxConcurrency = meta.MaxConcurrency.Value;
                    }

                    if (meta.MaxLength.HasValue)
                    {
                        settings.MaxLength = meta.MaxLength.Value;
                    }

                    if (!string.IsNullOrWhiteSpace(meta.EngineVersion))
                    {
                        settings.EngineVersion = meta.EngineVersion;
                    }
                }
            }
            catch
            {
            }
        }

        return (settings, modelConfig);
    }
}

public sealed class TranslationMeta
{
    public bool DisableTranslation { get; set; }
    public int? MaxConcurrency { get; set; }
    public int? MaxLength { get; set; }
    public string? EngineVersion { get; set; }
}
