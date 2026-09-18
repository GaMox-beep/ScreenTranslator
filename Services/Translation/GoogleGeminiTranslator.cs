using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ScreenTranslator.Services.Storage;

namespace ScreenTranslator.Services.Translation;

public class GoogleGeminiTranslator : ITranslator
{
    private static readonly Lazy<HttpClient> LazyDefaultHttpClient = new(() => new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(15)
    });

    private static readonly string[] DefaultFallbackModels =
    [
        "models/gemini-3.6-flash",
        "models/gemini-3.5-flash",
        "models/gemini-3.5-flash-lite",
        "models/gemini-3.1-flash-lite",
        "models/gemini-flash-latest"
    ];

    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;

    // Quản lý thời gian cooldown cho từng model khi bị hết quota (RPM/RPD) hoặc quá tải
    private readonly ConcurrentDictionary<string, DateTime> _modelCooldowns = new(StringComparer.OrdinalIgnoreCase);

    // Ghi nhớ model đã kiểm chứng hoạt động thành công gần nhất để gọi trực tiếp (dưới 500ms)
    private string? _activeWorkingModel;
    private List<string>? _cachedDiscoveredModels;
    private string? _cachedApiKey;

    public GoogleGeminiTranslator(HttpClient? httpClient = null, ISettingsService? settingsService = null)
    {
        _httpClient = httpClient ?? LazyDefaultHttpClient.Value;
        _settingsService = settingsService ?? new JsonSettingsService();

        try
        {
            var saved = _settingsService.Load().LastWorkingModel;
            if (!string.IsNullOrWhiteSpace(saved))
            {
                _activeWorkingModel = saved;
            }
        }
        catch
        {
            // Bỏ qua lỗi đọc cài đặt ban đầu
        }
    }

    public async Task<string> TranslateAsync(
        string text,
        string targetLanguage,
        string sourceLanguage,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Chưa cấu hình API Key. Vui lòng nhập API Key Google Gemini trong phần Cài đặt.");
        }

        var trimmedKey = apiKey.Trim();
        var now = DateTime.UtcNow;

        // BƯỚC 1 (FAST PATH): Nếu đã có model hoạt động tốt từ trước và không bị cooldown, gọi trực tiếp ngay lập tức!
        if (!string.IsNullOrEmpty(_activeWorkingModel) &&
            (_cachedApiKey == null || _cachedApiKey == trimmedKey) &&
            (!_modelCooldowns.TryGetValue(_activeWorkingModel, out var cooldown) || cooldown <= now))
        {
            _cachedApiKey = trimmedKey;
            AppLogger.Log($"[GEMINI] Fast-Path engaged: Model=\"{_activeWorkingModel}\"");
            var swFast = Stopwatch.StartNew();
            try
            {
                var result = await CallGeminiAsync(_activeWorkingModel, text, targetLanguage, trimmedKey, cancellationToken);
                swFast.Stop();
                AppLogger.Log($"[GEMINI] Fast-Path request succeeded: Model=\"{_activeWorkingModel}\", Latency={swFast.ElapsedMilliseconds}ms, OutputChars={result.Length}");
                return result;
            }
            catch (Exception ex) when (IsQuotaOrUnavailableException(ex, out var duration))
            {
                swFast.Stop();
                AppLogger.Log($"[GEMINI] WARN: Fast-Path request failed: Model=\"{_activeWorkingModel}\", Elapsed={swFast.ElapsedMilliseconds}ms, Error=\"{ex.Message}\". Cooldown={duration.TotalSeconds:F0}s.");
                _modelCooldowns[_activeWorkingModel] = DateTime.UtcNow.Add(duration);
                _activeWorkingModel = null;
            }
        }

        // BƯỚC 2 (DISCOVERY & ROTATION): Dò tìm hoặc luân chuyển sang model khả dụng kế tiếp
        AppLogger.Log("[GEMINI] Starting model discovery and rotation sequence...");
        var swDiscovery = Stopwatch.StartNew();
        var allModels = await GetOrDiscoverTextOutModelsAsync(trimmedKey, cancellationToken);
        swDiscovery.Stop();
        AppLogger.Log($"[GEMINI] Model discovery completed in {swDiscovery.ElapsedMilliseconds} ms: Found {allModels.Count} candidate models.");

        var availableModels = allModels
            .Where(m => !_modelCooldowns.TryGetValue(m, out var until) || until <= DateTime.UtcNow)
            .ToList();

        if (availableModels.Count == 0)
        {
            AppLogger.Log("[GEMINI] WARN: All candidate models are on cooldown. Retrying models with earliest expiration.");
            availableModels = allModels
                .OrderBy(m => _modelCooldowns.TryGetValue(m, out var until) ? until : DateTime.MinValue)
                .ToList();
        }

        Exception? lastException = null;

        foreach (var model in availableModels)
        {
            AppLogger.Log($"[GEMINI] Testing candidate model: Model=\"{model}\"...");
            var attemptSw = Stopwatch.StartNew();

            try
            {
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                attemptCts.CancelAfter(TimeSpan.FromSeconds(10));

                var result = await CallGeminiAsync(model, text, targetLanguage, trimmedKey, attemptCts.Token);
                attemptSw.Stop();

                AppLogger.Log($"[GEMINI] SUCCESS: Model=\"{model}\" responded in {attemptSw.ElapsedMilliseconds} ms (OutputChars={result.Length}). Promoted to active Fast-Path.");
                _activeWorkingModel = model;
                _cachedApiKey = trimmedKey;
                _modelCooldowns.TryRemove(model, out _);
                _settingsService.SaveLastWorkingModel(model);
                return result;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                attemptSw.Stop();
                AppLogger.Log($"[GEMINI] TIMEOUT: Model=\"{model}\" exceeded 10s deadline (Elapsed={attemptSw.ElapsedMilliseconds}ms). Skipping.");
                _modelCooldowns[model] = DateTime.UtcNow.AddSeconds(45);
                lastException = new TimeoutException($"Model {model} exceeded 10s timeout.");
            }
            catch (Exception ex) when (IsQuotaOrUnavailableException(ex, out var cooldownDuration))
            {
                attemptSw.Stop();
                AppLogger.Log($"[GEMINI] FAIL: Model=\"{model}\" failed after {attemptSw.ElapsedMilliseconds} ms: \"{ex.Message}\". Cooldown={cooldownDuration.TotalSeconds:F0}s.");
                _modelCooldowns[model] = DateTime.UtcNow.Add(cooldownDuration);
                lastException = ex;
            }
        }

        if (lastException != null)
        {
            throw lastException;
        }

        throw new InvalidOperationException("Tất cả các model Google Gemini hiện tại đều hết hạn ngạch (RPM/RPD) hoặc quá tải. Vui lòng thử lại sau.");
    }

    private static bool IsQuotaOrUnavailableException(Exception ex, out TimeSpan cooldown)
    {
        var msg = ex.Message.ToLowerInvariant();

        // 1. Hết hạn mức gọi (Quota / Rate limit)
        if (msg.Contains("429") || msg.Contains("quota") || msg.Contains("resource_exhausted") || msg.Contains("rate limit"))
        {
            if (msg.Contains("day") || msg.Contains("daily") || msg.Contains("per day") || msg.Contains("rpd"))
            {
                cooldown = TimeSpan.FromHours(4);
            }
            else
            {
                cooldown = TimeSpan.FromSeconds(60);
            }
            return true;
        }

        // 2. Máy chủ quá tải (503 Service Unavailable / High demand)
        if (msg.Contains("503") || msg.Contains("high demand") || msg.Contains("overloaded") || msg.Contains("unavailable"))
        {
            cooldown = TimeSpan.FromSeconds(30);
            return true;
        }

        // 3. Model không khả dụng hoặc tham số không tương thích (404, 400, Not Supported, Invalid Argument)
        if (msg.Contains("404") || msg.Contains("400") || msg.Contains("bad request") || msg.Contains("invalid argument") || msg.Contains("not found") || msg.Contains("no longer available") || msg.Contains("not supported"))
        {
            cooldown = TimeSpan.FromHours(2);
            return true;
        }

        // 4. Lỗi máy chủ hoặc timeout
        if (msg.Contains("500") || msg.Contains("timeout"))
        {
            cooldown = TimeSpan.FromSeconds(20);
            return true;
        }

        cooldown = TimeSpan.Zero;
        return false;
    }

    private async Task<List<string>> GetOrDiscoverTextOutModelsAsync(string apiKey, CancellationToken cancellationToken)
    {
        if (_cachedDiscoveredModels != null && _cachedDiscoveredModels.Count > 0 && _cachedApiKey == apiKey)
        {
            return _cachedDiscoveredModels;
        }

        var discovered = new List<string>();

        try
        {
            var listUrl = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
            using var request = new HttpRequestMessage(HttpMethod.Get, listUrl);
            request.Headers.Add("x-goog-api-key", apiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("models", out var modelsArray))
                {
                    foreach (var m in modelsArray.EnumerateArray())
                    {
                        if (m.TryGetProperty("name", out var nameProp))
                        {
                            var name = nameProp.GetString();
                            if (string.IsNullOrEmpty(name)) continue;

                            var lower = name.ToLowerInvariant();

                            if (!lower.Contains("gemini")) continue;

                            if (lower.Contains("embedding") ||
                                lower.Contains("aqa") ||
                                lower.Contains("imagen") ||
                                lower.Contains("veo") ||
                                lower.Contains("chirp") ||
                                lower.Contains("tts") ||
                                lower.Contains("robotics") ||
                                lower.Contains("deprecated"))
                            {
                                continue;
                            }

                            var supportsGenerate = true;
                            if (m.TryGetProperty("supportedGenerationMethods", out var methods))
                            {
                                supportsGenerate = false;
                                foreach (var method in methods.EnumerateArray())
                                {
                                    if (method.GetString() == "generateContent")
                                    {
                                        supportsGenerate = true;
                                        break;
                                    }
                                }
                            }

                            if (supportsGenerate)
                            {
                                discovered.Add(name);
                            }
                        }
                    }
                }
            }
            else
            {
                var errorMsg = ParseErrorMessage(response.StatusCode, json);
                throw new InvalidOperationException(errorMsg);
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            // Bỏ qua lỗi mạng khi list models để fallback sang danh sách mặc định bên dưới
        }

        var sorted = discovered
            .OrderByDescending(CalculateModelPriorityScore)
            .ToList();

        if (sorted.Count == 0)
        {
            sorted.AddRange(DefaultFallbackModels);
        }

        _cachedDiscoveredModels = sorted;
        _cachedApiKey = apiKey;
        return sorted;
    }

    private static double CalculateModelPriorityScore(string modelName)
    {
        var lower = modelName.ToLowerInvariant();
        double score = 0;

        var match = Regex.Match(lower, @"gemini-(\d+(?:\.\d+)?)");
        if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var version))
        {
            score += version * 100;
        }

        if (lower.Contains("flash-lite") || lower.Contains("flash_lite"))
        {
            score += 50;
        }
        else if (lower.Contains("flash"))
        {
            score += 30;
        }
        else if (lower.Contains("pro"))
        {
            score += 10;
        }

        if (lower.Contains("exp") || lower.Contains("preview"))
        {
            score -= 15;
        }

        return score;
    }

    private async Task<string> CallGeminiAsync(
        string modelWithPrefix,
        string text,
        string targetLanguage,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var normalizedModel = modelWithPrefix.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? modelWithPrefix
            : $"models/{modelWithPrefix}";

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/{normalizedModel}:generateContent?key={apiKey}";

        var prompt = $"Translate the following text into {targetLanguage} accurately and naturally. " +
                     "The text is captured via screen OCR and may contain minor recognition artifacts, fused words, or typos; " +
                     "contextually infer and restore the intended wording before translating. " +
                     "Maintain original paragraphing and line breaks. " +
                     "Output ONLY the translated text directly without any explanation, greetings, notes, quotes, or markdown code blocks:\n\n" +
                     text;

        // Thử gọi với thinkingConfig (thinkingBudget = 0) để triệt tiêu thời gian suy nghĩ
        try
        {
            return await ExecuteGeminiRequestAsync(endpoint, prompt, normalizedModel, includeThinkingConfig: true, apiKey, cancellationToken);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("400", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("thinking", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("invalid argument", StringComparison.OrdinalIgnoreCase))
        {
            // Nếu model này không hỗ trợ thinkingConfig (như flash-lite hoặc 3.8), tự động thử lại bằng cấu hình tiêu chuẩn
            return await ExecuteGeminiRequestAsync(endpoint, prompt, normalizedModel, includeThinkingConfig: false, apiKey, cancellationToken);
        }
    }

    private async Task<string> ExecuteGeminiRequestAsync(
        string endpoint,
        string prompt,
        string model,
        bool includeThinkingConfig,
        string apiKey,
        CancellationToken cancellationToken)
    {
        object generationConfig;

        if (includeThinkingConfig)
        {
            if (model.Contains("3.6") || model.Contains("3.7"))
            {
                generationConfig = new
                {
                    temperature = 0.1,
                    maxOutputTokens = 4096,
                    thinkingConfig = new
                    {
                        thinkingLevel = "minimal"
                    }
                };
            }
            else
            {
                generationConfig = new
                {
                    temperature = 0.1,
                    maxOutputTokens = 4096,
                    thinkingConfig = new
                    {
                        thinkingBudget = 0
                    }
                };
            }
        }
        else
        {
            generationConfig = new
            {
                temperature = 0.1,
                maxOutputTokens = 4096
            };
        }

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("x-goog-api-key", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            throw new TimeoutException("Hết thời gian chờ phản hồi từ Google Gemini (Timeout).");
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"Lỗi kết nối mạng đến Google: {ex.Message}", ex);
        }

        using (response)
        {
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = ParseErrorMessage(response.StatusCode, responseJson);
                throw new InvalidOperationException(errorMessage);
            }

            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0)
                {
                    var firstPart = parts[0];
                    if (firstPart.TryGetProperty("text", out var textElement))
                    {
                        var result = textElement.GetString()?.Trim() ?? string.Empty;
                        return result;
                    }
                }
            }

            throw new InvalidOperationException("Google Gemini không trả về nội dung dịch.");
        }
    }

    private static string ParseErrorMessage(System.Net.HttpStatusCode statusCode, string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("error", out var errorProp))
            {
                if (errorProp.TryGetProperty("message", out var msgProp))
                {
                    var detail = msgProp.GetString();
                    if (!string.IsNullOrWhiteSpace(detail))
                    {
                        return $"Google ({((int)statusCode)}): {detail}";
                    }
                }
            }
        }
        catch
        {
            // Bỏ qua nếu không parse được JSON lỗi
        }

        return statusCode switch
        {
            System.Net.HttpStatusCode.BadRequest => "Lỗi 400: Yêu cầu hoặc API Key không hợp lệ.",
            System.Net.HttpStatusCode.Unauthorized => "Lỗi 401: API Key Google không chính xác hoặc đã hết hạn.",
            System.Net.HttpStatusCode.Forbidden => "Lỗi 403: Không có quyền truy cập Google Gemini API.",
            System.Net.HttpStatusCode.NotFound => "Lỗi 404: Không tìm thấy model tương thích trên tài khoản Google này.",
            System.Net.HttpStatusCode.ServiceUnavailable => "Lỗi 503: Máy chủ Google đang quá tải (High Demand).",
            System.Net.HttpStatusCode.TooManyRequests => "Lỗi 429: Đã vượt quá giới hạn lượt gọi (RPM/RPD) của Google AI Studio.",
            _ => $"Lỗi {(int)statusCode} từ Google Gemini API."
        };
    }
}
