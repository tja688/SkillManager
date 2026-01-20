using SkillManager.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SkillManager.Services;

public sealed class TranslationService : IDisposable
{
    private readonly TranslationCacheStore _cacheStore;
    private readonly ITranslationProvider _provider;
    private readonly TranslationSettings _settings;
    private readonly TranslationTermProtector _termProtector;
    private readonly Channel<TranslationJob> _queue;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _workers = new();
    private readonly DebugService _debugService;

    public TranslationService(
        TranslationCacheStore cacheStore,
        ITranslationProvider provider,
        TranslationSettings settings,
        TranslationTermProtector termProtector)
    {
        _cacheStore = cacheStore;
        _provider = provider;
        _settings = settings;
        _termProtector = termProtector;
        _debugService = DebugService.Instance;

        _queue = Channel.CreateUnbounded<TranslationJob>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        var workers = Math.Clamp(_settings.MaxConcurrency, 1, 2);
        for (var i = 0; i < workers; i++)
        {
            _workers.Add(Task.Run(() => WorkerAsync(_cts.Token)));
        }
    }

    public event EventHandler<TranslationQueuedEventArgs>? TranslationQueued;
    public event EventHandler<TranslationCompletedEventArgs>? TranslationCompleted;

    public async Task<IReadOnlyDictionary<string, TranslationPair>> GetCachedTranslationsAsync(IEnumerable<SkillFolder> skills, CancellationToken ct)
    {
        var keys = new List<(string SkillId, string Field, TranslationKey Key)>();
        foreach (var skill in skills)
        {
            var skillId = NormalizeSkillId(skill.FullPath);
            AddKeyIfNeeded(keys, skillId, TranslationFields.WhenToUse, skill.WhenToUse);
            AddKeyIfNeeded(keys, skillId, TranslationFields.Description, skill.Description);
        }

        var records = await _cacheStore.GetBatchAsync(keys.Select(k => k.Key), ct);
        var result = new Dictionary<string, TranslationPair>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in keys)
        {
            if (records.TryGetValue(item.Key.CacheKey, out var record) && record.Status == TranslationStatus.Ready)
            {
                if (!result.TryGetValue(item.SkillId, out var pair))
                {
                    pair = new TranslationPair();
                    result[item.SkillId] = pair;
                }

                if (item.Field == TranslationFields.WhenToUse)
                {
                    pair.WhenToUse = record.TranslatedText;
                }
                else if (item.Field == TranslationFields.Description)
                {
                    pair.Description = record.TranslatedText;
                }
            }
        }

        return result;
    }

    public async Task QueueIncrementalAsync(IEnumerable<SkillFolder> skills, CancellationToken ct)
    {
        if (!_settings.EnableTranslation) return;
        var jobs = await BuildJobsAsync(skills, retryFailed: false, ct);
        foreach (var job in jobs)
        {
            await EnqueueAsync(job, ct);
        }
    }

    public async Task RunBatchPretranslateAsync(IEnumerable<SkillFolder> skills, IProgress<TranslationProgressInfo>? progress, CancellationToken ct)
    {
        if (!_settings.EnableTranslation) return;

        var jobs = await BuildJobsAsync(skills, retryFailed: true, ct);
        var total = jobs.Count;
        var completed = 0;
        var failed = 0;

        foreach (var job in jobs)
        {
            await EnqueueAsync(job, ct);
        }

        foreach (var job in jobs)
        {
            ct.ThrowIfCancellationRequested();
            TranslationRecord? record = null;
            var failedRecorded = false;
            try
            {
                record = await job.Completion!.Task.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                failed++;
                failedRecorded = true;
            }

            if (!failedRecorded && record?.Status == TranslationStatus.Failed)
            {
                failed++;
            }

            completed++;
            progress?.Report(new TranslationProgressInfo
            {
                Total = total,
                Completed = completed,
                Failed = failed,
                CurrentSkillName = job.SkillName,
                CurrentField = job.Field
            });
        }
    }

    public async Task DeleteCacheAsync(CancellationToken ct)
    {
        await _cacheStore.DeleteAllAsync(ct);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _queue.Writer.TryComplete();
    }

    public static string NormalizeSkillId(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        return path.Trim()
            .Replace('\\', '/')
            .ToLowerInvariant();
    }

    private async Task<List<TranslationJob>> BuildJobsAsync(IEnumerable<SkillFolder> skills, bool retryFailed, CancellationToken ct)
    {
        var candidates = new List<(string SkillId, string SkillName, string Field, string Text, TranslationKey Key)>();
        foreach (var skill in skills)
        {
            var skillId = NormalizeSkillId(skill.FullPath);
            AddCandidateIfNeeded(candidates, skillId, skill.Name, TranslationFields.WhenToUse, skill.WhenToUse);
            AddCandidateIfNeeded(candidates, skillId, skill.Name, TranslationFields.Description, skill.Description);
        }

        var records = await _cacheStore.GetBatchAsync(candidates.Select(c => c.Key), ct);
        var jobs = new List<TranslationJob>();

        foreach (var candidate in candidates)
        {
            if (records.TryGetValue(candidate.Key.CacheKey, out var record))
            {
                if (record.Status == TranslationStatus.Ready) continue;
                if (record.Status == TranslationStatus.Failed && !retryFailed) continue;
            }

            jobs.Add(new TranslationJob
            {
                SkillId = candidate.SkillId,
                SkillName = candidate.SkillName,
                Field = candidate.Field,
                SourceText = candidate.Text,
                SourceHash = candidate.Key.SourceHash,
                CancellationToken = ct,
                Completion = new TaskCompletionSource<TranslationRecord?>(TaskCreationOptions.RunContinuationsAsynchronously)
            });
        }

        return jobs;
    }

    private async Task EnqueueAsync(TranslationJob job, CancellationToken ct)
    {
        TranslationQueued?.Invoke(this, new TranslationQueuedEventArgs(job.SkillId, job.Field));
        await _queue.Writer.WriteAsync(job, ct);
    }

    private async Task WorkerAsync(CancellationToken ct)
    {
        await foreach (var job in _queue.Reader.ReadAllAsync(ct))
        {
            if (job.CancellationToken.IsCancellationRequested)
            {
                job.Completion?.TrySetCanceled(job.CancellationToken);
                continue;
            }

            TranslationRecord? record = null;
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, job.CancellationToken);
                record = await TranslateJobAsync(job, linkedCts.Token);
                job.Completion?.TrySetResult(record);
            }
            catch (OperationCanceledException oce)
            {
                job.Completion?.TrySetCanceled(oce.CancellationToken);
            }
            catch (Exception ex)
            {
                job.Completion?.TrySetException(ex);
                _debugService.LogIfEnabled(
                    "scroll_viewmodel_state",
                    "Translation-Error",
                    ex.Message,
                    "TranslationService",
                    DebugLogLevel.Error);
            }
        }
    }

    private async Task<TranslationRecord> TranslateJobAsync(TranslationJob job, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        var key = new TranslationKey(
            job.SkillId,
            job.Field,
            _settings.TargetLang,
            _settings.EngineId,
            _settings.EngineVersion,
            job.SourceHash);

        try
        {
            var protectedText = _termProtector.Protect(job.SourceText);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (_settings.Timeout > TimeSpan.Zero)
            {
                timeoutCts.CancelAfter(_settings.Timeout);
            }

            var translated = await _provider.TranslateAsync(
                protectedText.Text,
                _settings.SourceLang,
                _settings.TargetLang,
                new TranslationOptions { MaxLength = _settings.MaxLength },
                timeoutCts.Token);

            var restored = _termProtector.Restore(translated, protectedText);

            var record = new TranslationRecord
            {
                SkillId = job.SkillId,
                Field = job.Field,
                TargetLang = _settings.TargetLang,
                EngineId = _settings.EngineId,
                EngineVersion = _settings.EngineVersion,
                SourceHash = job.SourceHash,
                TranslatedText = restored,
                CreatedAt = started,
                UpdatedAt = DateTime.UtcNow,
                Status = TranslationStatus.Ready
            };

            await _cacheStore.UpsertAsync(record, ct);
            TranslationCompleted?.Invoke(this, new TranslationCompletedEventArgs(job.SkillId, job.Field, true, restored, null));
            return record;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var record = new TranslationRecord
            {
                SkillId = job.SkillId,
                Field = job.Field,
                TargetLang = _settings.TargetLang,
                EngineId = _settings.EngineId,
                EngineVersion = _settings.EngineVersion,
                SourceHash = job.SourceHash,
                TranslatedText = string.Empty,
                CreatedAt = started,
                UpdatedAt = DateTime.UtcNow,
                Status = TranslationStatus.Failed,
                Error = ex.Message
            };

            await _cacheStore.UpsertAsync(record, ct);
            TranslationCompleted?.Invoke(this, new TranslationCompletedEventArgs(job.SkillId, job.Field, false, null, ex.Message));
            return record;
        }
    }

    private void AddKeyIfNeeded(List<(string SkillId, string Field, TranslationKey Key)> keys, string skillId, string field, string text)
    {
        if (!ShouldTranslate(text)) return;
        var sourceHash = ComputeSourceHash(text);
        keys.Add((skillId, field, new TranslationKey(skillId, field, _settings.TargetLang, _settings.EngineId, _settings.EngineVersion, sourceHash)));
    }

    private void AddCandidateIfNeeded(List<(string SkillId, string SkillName, string Field, string Text, TranslationKey Key)> candidates, string skillId, string skillName, string field, string text)
    {
        if (!ShouldTranslate(text)) return;
        var sourceHash = ComputeSourceHash(text);
        candidates.Add((skillId, skillName, field, text, new TranslationKey(skillId, field, _settings.TargetLang, _settings.EngineId, _settings.EngineVersion, sourceHash)));
    }

    private static bool ShouldTranslate(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (text.Length < 3) return false;
        return text.Any(static c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
    }

    private static string ComputeSourceHash(string text)
    {
        var normalized = text.Trim().Replace("\r\n", "\n");
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private sealed class TranslationJob
    {
        public string SkillId { get; init; } = string.Empty;
        public string SkillName { get; init; } = string.Empty;
        public string Field { get; init; } = string.Empty;
        public string SourceText { get; init; } = string.Empty;
        public string SourceHash { get; init; } = string.Empty;
        public CancellationToken CancellationToken { get; init; }
        public TaskCompletionSource<TranslationRecord?>? Completion { get; init; }
    }
}
