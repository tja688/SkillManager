using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SkillManager.Services;

/// <summary>
/// 远程翻译服务客户端 - 调用独立的 LocalTranslation 服务
/// </summary>
public sealed class RemoteTranslationProvider : ITranslationProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly DebugService _debugService;

    /// <summary>
    /// 创建远程翻译客户端
    /// </summary>
    /// <param name="baseUrl">翻译服务地址，默认 http://localhost:5123</param>
    /// <param name="timeoutSeconds">请求超时时间</param>
    public RemoteTranslationProvider(string baseUrl = "http://localhost:5123", int timeoutSeconds = 120)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        _debugService = DebugService.Instance;
    }

    /// <summary>
    /// 检查翻译服务是否可用
    /// </summary>
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/translate/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _debugService.Log("Translation", $"Service unavailable: {ex.Message}", "RemoteTranslationProvider", DebugLogLevel.Warning);
            return false;
        }
    }

    /// <summary>
    /// 获取服务状态
    /// </summary>
    public async Task<RemoteTranslationStatus?> GetStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/translate/status", ct);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<RemoteTranslationStatus>(_jsonOptions, ct);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 实现 ITranslationProvider 接口
    /// </summary>
    public async Task<string> TranslateAsync(string text, string sourceLang, string targetLang, TranslationOptions options, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        try
        {
            var request = new RemoteTranslateRequest
            {
                Text = text,
                SourceLang = sourceLang,
                TargetLang = targetLang,
                MaxLength = options.MaxLength
            };

            _debugService.Log("Translation", $"Sending translation request: {text[..Math.Min(50, text.Length)]}...", "RemoteTranslationProvider", DebugLogLevel.Info);

            var response = await _httpClient.PostAsJsonAsync("/api/translate", request, _jsonOptions, ct);
            var result = await response.Content.ReadFromJsonAsync<RemoteTranslateResponse>(_jsonOptions, ct);

            if (result?.Success == true && !string.IsNullOrEmpty(result.TranslatedText))
            {
                _debugService.Log("Translation", $"Translation succeeded: {result.TranslatedText[..Math.Min(50, result.TranslatedText.Length)]}...", "RemoteTranslationProvider", DebugLogLevel.Info);
                return result.TranslatedText;
            }

            var error = result?.Error ?? "Unknown error";
            _debugService.Log("Translation", $"Translation failed: {error}", "RemoteTranslationProvider", DebugLogLevel.Warning);
            throw new TranslationException(error);
        }
        catch (HttpRequestException ex)
        {
            var error = $"翻译服务连接失败: {ex.Message}。请确保 LocalTranslation 服务正在运行 (http://localhost:5123)";
            _debugService.Log("Translation", error, "RemoteTranslationProvider", DebugLogLevel.Error);
            throw new TranslationException(error, ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || !ct.IsCancellationRequested)
        {
            var error = "翻译请求超时";
            _debugService.Log("Translation", error, "RemoteTranslationProvider", DebugLogLevel.Warning);
            throw new TranslationException(error, ex);
        }
        catch (TranslationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _debugService.Log("Translation", $"Unexpected error: {ex.Message}", "RemoteTranslationProvider", DebugLogLevel.Error);
            throw new TranslationException($"翻译失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 批量翻译
    /// </summary>
    public async Task<Dictionary<string, string>> BatchTranslateAsync(
        IEnumerable<(string Id, string Text)> items,
        string sourceLang,
        string targetLang,
        int? maxLength = null,
        CancellationToken ct = default)
    {
        var results = new Dictionary<string, string>();
        var itemList = items.ToList();

        if (itemList.Count == 0)
        {
            return results;
        }

        try
        {
            var request = new RemoteBatchTranslateRequest
            {
                Items = itemList.Select(i => new RemoteBatchTranslateItem { Id = i.Id, Text = i.Text }).ToList(),
                SourceLang = sourceLang,
                TargetLang = targetLang,
                MaxLength = maxLength
            };

            var response = await _httpClient.PostAsJsonAsync("/api/translate/batch", request, _jsonOptions, ct);
            var batchResult = await response.Content.ReadFromJsonAsync<RemoteBatchTranslateResponse>(_jsonOptions, ct);

            if (batchResult?.Results != null)
            {
                foreach (var item in batchResult.Results)
                {
                    if (item.Success && !string.IsNullOrEmpty(item.TranslatedText))
                    {
                        results[item.Id] = item.TranslatedText;
                    }
                }
            }

            _debugService.Log("Translation", $"Batch translation completed: {batchResult?.Succeeded ?? 0}/{batchResult?.Total ?? 0} succeeded", "RemoteTranslationProvider", DebugLogLevel.Info);
        }
        catch (Exception ex)
        {
            _debugService.Log("Translation", $"Batch translation failed: {ex.Message}", "RemoteTranslationProvider", DebugLogLevel.Error);
        }

        return results;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    #region Request/Response Models

    private sealed class RemoteTranslateRequest
    {
        public string Text { get; set; } = string.Empty;
        public string? SourceLang { get; set; }
        public string? TargetLang { get; set; }
        public int? MaxLength { get; set; }
    }

    private sealed class RemoteTranslateResponse
    {
        public bool Success { get; set; }
        public string? TranslatedText { get; set; }
        public string? Error { get; set; }
        public string? SourceLang { get; set; }
        public string? TargetLang { get; set; }
        public long ElapsedMs { get; set; }
    }

    private sealed class RemoteBatchTranslateRequest
    {
        public List<RemoteBatchTranslateItem> Items { get; set; } = [];
        public string? SourceLang { get; set; }
        public string? TargetLang { get; set; }
        public int? MaxLength { get; set; }
    }

    private sealed class RemoteBatchTranslateItem
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    private sealed class RemoteBatchTranslateResponse
    {
        public int Total { get; set; }
        public int Succeeded { get; set; }
        public int Failed { get; set; }
        public List<RemoteBatchTranslateResultItem>? Results { get; set; }
        public long TotalElapsedMs { get; set; }
    }

    private sealed class RemoteBatchTranslateResultItem
    {
        public string Id { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? TranslatedText { get; set; }
        public string? Error { get; set; }
    }

    #endregion
}

/// <summary>
/// 远程服务状态
/// </summary>
public sealed class RemoteTranslationStatus
{
    public bool Ready { get; set; }
    public bool ModelLoaded { get; set; }
    public string? ModelDirectory { get; set; }
    public string? SourceLang { get; set; }
    public string? TargetLang { get; set; }
    public string? EngineVersion { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// 翻译异常
/// </summary>
public class TranslationException : Exception
{
    public TranslationException(string message) : base(message) { }
    public TranslationException(string message, Exception innerException) : base(message, innerException) { }
}
