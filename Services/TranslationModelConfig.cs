using System;
using System.IO;
using System.Text.Json;

namespace SkillManager.Services;

public sealed class TranslationModelConfig
{
    public string ModelDirectory { get; init; } = string.Empty;
    public string EncoderModelPath { get; init; } = string.Empty;
    public string DecoderModelPath { get; init; } = string.Empty;
    public string TokenizerPath { get; init; } = string.Empty;
    public string? SourcePrefix { get; init; }
    public int? BosTokenId { get; init; }
    public int? EosTokenId { get; init; }
    public int? PadTokenId { get; init; }
    public int? DecoderStartTokenId { get; init; }
    public int MaxLength { get; init; } = 96;
    public string EngineVersion { get; init; } = "unknown";

    public static TranslationModelConfig Load(string modelDirectory)
    {
        var configPath = Path.Combine(modelDirectory, "model.config.json");
        TranslationModelConfig config;

        if (File.Exists(configPath))
        {
            var json = File.ReadAllText(configPath);
            config = JsonSerializer.Deserialize<TranslationModelConfig>(json) ?? new TranslationModelConfig();
        }
        else
        {
            config = new TranslationModelConfig();
        }

        var normalized = new TranslationModelConfig
        {
            ModelDirectory = modelDirectory,
            EncoderModelPath = ResolvePath(modelDirectory, config.EncoderModelPath, "encoder_model.onnx"),
            DecoderModelPath = ResolvePath(modelDirectory, config.DecoderModelPath, "decoder_model.onnx"),
            TokenizerPath = ResolveTokenizerPath(modelDirectory, config.TokenizerPath),
            SourcePrefix = config.SourcePrefix,
            BosTokenId = config.BosTokenId,
            EosTokenId = config.EosTokenId,
            PadTokenId = config.PadTokenId,
            DecoderStartTokenId = config.DecoderStartTokenId,
            MaxLength = config.MaxLength > 0 ? config.MaxLength : 96,
            EngineVersion = ResolveEngineVersion(modelDirectory, config.EngineVersion)
        };

        return normalized;
    }

    private static string ResolvePath(string modelDirectory, string path, string fallbackFileName)
    {
        var candidate = string.IsNullOrWhiteSpace(path) ? fallbackFileName : path;
        return Path.IsPathRooted(candidate) ? candidate : Path.Combine(modelDirectory, candidate);
    }

    private static string ResolveTokenizerPath(string modelDirectory, string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            return ResolvePath(modelDirectory, path, path);
        }

        var candidates = new[]
        {
            "tokenizer.model",
            "tokenizer.spm",
            "sentencepiece.model",
            "tokenizer.json"
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.Combine(modelDirectory, candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return Path.Combine(modelDirectory, "tokenizer.model");
    }

    private static string ResolveEngineVersion(string modelDirectory, string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var versionPath = Path.Combine(modelDirectory, "engine.version");
        if (File.Exists(versionPath))
        {
            return File.ReadAllText(versionPath).Trim();
        }

        if (Directory.Exists(modelDirectory))
        {
            return Directory.GetLastWriteTimeUtc(modelDirectory).ToString("yyyyMMddHHmmss");
        }

        return "unknown";
    }
}
