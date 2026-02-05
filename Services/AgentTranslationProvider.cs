using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SkillManager.Services;

/// <summary>
/// 代理翻译服务提供者 - 调用本地部署的 AI 翻译服务 (Ollama/Qwen3)
/// </summary>
public sealed class AgentTranslationProvider : ITranslationProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly DebugService _debugService;

    public AgentTranslationProvider(string baseUrl = "http://127.0.0.1:8080", int timeoutSeconds = 60)
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

    public async Task<string> TranslateAsync(string text, string sourceLang, string targetLang, TranslationOptions options, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        try
        {
            var request = new TranslateRequest
            {
                Text = text,
                TargetLang = targetLang == "zh-CN" ? "Simplified Chinese" : targetLang
            };

            _debugService.Log("Translation", $"Requesting AI translation for: {text[..Math.Min(30, text.Length)]}...", "AgentTranslationProvider", DebugLogLevel.Info);

            var response = await _httpClient.PostAsJsonAsync("/translate", request, _jsonOptions, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TranslateResponse>(_jsonOptions, ct);

            if (result?.Status == "success" && !string.IsNullOrEmpty(result.TranslatedText))
            {
                _debugService.Log("Translation", "AI Translation succeeded.", "AgentTranslationProvider", DebugLogLevel.Info);
                return result.TranslatedText;
            }

            var error = result?.Status ?? "Unknown error from service";
            _debugService.Log("Translation", $"AI Translation failed: {error}", "AgentTranslationProvider", DebugLogLevel.Warning);
            throw new Exception($"Translation service failed: {error}");
        }
        catch (Exception ex)
        {
            _debugService.Log("Translation", $"AI Translation service error: {ex.Message}", "AgentTranslationProvider", DebugLogLevel.Error);
            throw; // Propagate exception to let TranslationService handle it as a failure
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private sealed class TranslateRequest
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("target_lang")]
        public string TargetLang { get; set; } = "Simplified Chinese";
    }

    private sealed class TranslateResponse
    {
        [JsonPropertyName("translated_text")]
        public string? TranslatedText { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }
}
