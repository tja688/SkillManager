using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SkillManager.Services;

public class TranslationCacheStore
{
    private readonly string _cachePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private TranslationCacheFile _cache = new();
    private bool _loaded;

    public TranslationCacheStore(string cachePath)
    {
        _cachePath = cachePath;
    }

    public async Task<TranslationRecord?> TryGetAsync(TranslationKey key, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);
        await _lock.WaitAsync(ct);
        try
        {
            return _cache.Records.TryGetValue(key.CacheKey, out var record) ? record : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyDictionary<string, TranslationRecord>> GetBatchAsync(IEnumerable<TranslationKey> keys, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);
        var results = new Dictionary<string, TranslationRecord>(StringComparer.Ordinal);
        await _lock.WaitAsync(ct);
        try
        {
            foreach (var key in keys)
            {
                if (_cache.Records.TryGetValue(key.CacheKey, out var record))
                {
                    results[key.CacheKey] = record;
                }
            }
        }
        finally
        {
            _lock.Release();
        }

        return results;
    }

    public async Task UpsertAsync(TranslationRecord record, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);
        await _lock.WaitAsync(ct);
        try
        {
            _cache.Records[BuildCacheKey(record)] = record;
            await SaveAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpsertRangeAsync(IEnumerable<TranslationRecord> records, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);
        await _lock.WaitAsync(ct);
        try
        {
            foreach (var record in records)
            {
                _cache.Records[BuildCacheKey(record)] = record;
            }

            await SaveAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAllAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            _cache = new TranslationCacheFile();
            _loaded = true;
            if (File.Exists(_cachePath))
            {
                File.Delete(_cachePath);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_loaded) return;
        await _lock.WaitAsync(ct);
        try
        {
            if (_loaded) return;

            if (!File.Exists(_cachePath))
            {
                _cache = new TranslationCacheFile();
                _loaded = true;
                return;
            }

            var json = await File.ReadAllTextAsync(_cachePath, ct);
            _cache = JsonSerializer.Deserialize<TranslationCacheFile>(json) ?? new TranslationCacheFile();
            _loaded = true;
        }
        catch
        {
            try
            {
                var backupPath = $"{_cachePath}.broken_{DateTime.UtcNow:yyyyMMddHHmmss}";
                File.Move(_cachePath, backupPath, true);
            }
            catch { }

            _cache = new TranslationCacheFile();
            _loaded = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_cachePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = false });
        await File.WriteAllTextAsync(_cachePath, json, ct);
    }

    private static string BuildCacheKey(TranslationRecord record)
    {
        return $"{record.SkillId}|{record.Field}|{record.TargetLang}|{record.EngineId}|{record.EngineVersion}|{record.SourceHash}";
    }
}
