using SkillManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SkillManager.Services;

public sealed class ManualTranslationStore
{
    private readonly string _filePath;
    private readonly string _libraryPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public ManualTranslationStore(string filePath, string libraryPath)
    {
        _filePath = filePath;
        _libraryPath = libraryPath;
    }

    public string FilePath => _filePath;

    public async Task<IReadOnlyDictionary<string, TranslationPair>> SyncAndLoadAsync(IEnumerable<SkillFolder> skills, CancellationToken ct)
    {
        var skillList = skills.Where(skill => !string.IsNullOrWhiteSpace(skill.Name)).ToList();
        var data = await LoadAsync(ct);
        var updated = MergeSkills(data, skillList);
        if (updated)
        {
            await SaveAsync(data, ct);
        }

        return BuildTranslationMap(data, skillList);
    }

    private async Task<ManualTranslationFile> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
        {
            return new ManualTranslationFile { LibraryPath = _libraryPath };
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, ct);
            return JsonSerializer.Deserialize<ManualTranslationFile>(json) ?? new ManualTranslationFile { LibraryPath = _libraryPath };
        }
        catch
        {
            try
            {
                var backupPath = $"{_filePath}.broken_{DateTime.UtcNow:yyyyMMddHHmmss}";
                File.Move(_filePath, backupPath, true);
            }
            catch
            {
            }

            return new ManualTranslationFile { LibraryPath = _libraryPath };
        }
    }

    private async Task SaveAsync(ManualTranslationFile data, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(data, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json, ct);
    }

    private bool MergeSkills(ManualTranslationFile data, List<SkillFolder> skills)
    {
        var updated = false;
        if (!string.Equals(data.LibraryPath, _libraryPath, StringComparison.Ordinal))
        {
            data.LibraryPath = _libraryPath;
            updated = true;
        }

        var byName = data.Skills.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        var byId = data.Skills
            .Where(s => !string.IsNullOrWhiteSpace(s.Id))
            .ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);

        var activeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var skill in skills)
        {
            activeNames.Add(skill.Name);

            ManualTranslationSkill? entry = null;
            if (!string.IsNullOrWhiteSpace(skill.SkillId) && byId.TryGetValue(skill.SkillId, out var matchedById))
            {
                entry = matchedById;
            }
            else if (byName.TryGetValue(skill.Name, out var matchedByName))
            {
                entry = matchedByName;
            }

            if (entry == null)
            {
                entry = new ManualTranslationSkill
                {
                    Id = skill.SkillId,
                    Name = skill.Name,
                    Path = skill.FullPath,
                    Description = new ManualTranslationField { Source = skill.Description },
                    WhenToUse = new ManualTranslationField { Source = skill.WhenToUse }
                };
                data.Skills.Add(entry);
                updated = true;
                continue;
            }

            if (!string.Equals(entry.Id, skill.SkillId, StringComparison.Ordinal))
            {
                entry.Id = skill.SkillId;
                updated = true;
            }

            if (!string.Equals(entry.Name, skill.Name, StringComparison.Ordinal))
            {
                entry.Name = skill.Name;
                updated = true;
            }

            if (!string.Equals(entry.Path, skill.FullPath, StringComparison.Ordinal))
            {
                entry.Path = skill.FullPath;
                updated = true;
            }

            entry.Description ??= new ManualTranslationField();
            entry.WhenToUse ??= new ManualTranslationField();

            if (!string.Equals(entry.Description.Source, skill.Description, StringComparison.Ordinal))
            {
                entry.Description.Source = skill.Description;
                updated = true;
            }

            if (!string.Equals(entry.WhenToUse.Source, skill.WhenToUse, StringComparison.Ordinal))
            {
                entry.WhenToUse.Source = skill.WhenToUse;
                updated = true;
            }
        }

        if (data.Skills.RemoveAll(s => !activeNames.Contains(s.Name)) > 0)
        {
            updated = true;
        }

        var ordered = data.Skills.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
        if (!data.Skills.Select(s => s.Name).SequenceEqual(ordered.Select(s => s.Name), StringComparer.OrdinalIgnoreCase))
        {
            data.Skills = ordered;
            updated = true;
        }

        if (updated)
        {
            data.GeneratedAtUtc = DateTime.UtcNow;
        }

        return updated;
    }

    private static IReadOnlyDictionary<string, TranslationPair> BuildTranslationMap(
        ManualTranslationFile data,
        List<SkillFolder> skills)
    {
        var result = new Dictionary<string, TranslationPair>(StringComparer.OrdinalIgnoreCase);
        var byName = data.Skills.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        var byId = data.Skills
            .Where(s => !string.IsNullOrWhiteSpace(s.Id))
            .ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var skill in skills)
        {
            if (string.IsNullOrWhiteSpace(skill.SkillId))
            {
                continue;
            }

            ManualTranslationSkill? entry = null;
            if (!string.IsNullOrWhiteSpace(skill.SkillId) && byId.TryGetValue(skill.SkillId, out var matchedById))
            {
                entry = matchedById;
            }
            else if (byName.TryGetValue(skill.Name, out var matchedByName))
            {
                entry = matchedByName;
            }

            if (entry == null)
            {
                continue;
            }

            var whenToUseTranslation = entry.WhenToUse?.Translation ?? string.Empty;
            var descriptionTranslation = entry.Description?.Translation ?? string.Empty;

            if (string.IsNullOrWhiteSpace(whenToUseTranslation) && string.IsNullOrWhiteSpace(descriptionTranslation))
            {
                continue;
            }

            result[skill.SkillId] = new TranslationPair
            {
                WhenToUse = whenToUseTranslation,
                Description = descriptionTranslation
            };
        }

        return result;
    }
}
