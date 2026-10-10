using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace ScreenTranslator.Services.Translation;

internal sealed class GeminiApiException : InvalidOperationException
{
    private static readonly string[] ThinkingFields =
    [
        "thinkingConfig", "thinking_config", "thinkingBudget", "thinking_budget",
        "thinkingLevel", "thinking_level", "thinking level",
        "does not support thinking", "thinking is not supported"
    ];
    public HttpStatusCode StatusCode { get; }
    public TimeSpan? RetryAfter { get; }
    public bool IsDailyQuota { get; }
    public bool IsThinkingConfigurationError { get; }

    private GeminiApiException(HttpStatusCode status, TimeSpan? retryAfter, bool dailyQuota, bool thinkingError)
        : base(status switch
        {
            HttpStatusCode.BadRequest => "Google (400): Yêu cầu không hợp lệ. Kiểm tra API key và cấu hình dịch.",
            HttpStatusCode.Unauthorized => "Google (401): API key không hợp lệ hoặc đã hết hạn.",
            HttpStatusCode.Forbidden => "Google (403): API key không có quyền sử dụng Gemini.",
            HttpStatusCode.NotFound => "Google (404): Model này không còn khả dụng.",
            HttpStatusCode.TooManyRequests => "Google (429): Đã hết hạn ngạch. Vui lòng thử lại sau.",
            HttpStatusCode.ServiceUnavailable => "Google (503): Máy chủ đang quá tải. Vui lòng thử lại sau.",
            _ => $"Google ({(int)status}): Không thể hoàn tất bản dịch. Vui lòng thử lại sau."
        })
    {
        StatusCode = status;
        RetryAfter = retryAfter;
        IsDailyQuota = dailyQuota;
        IsThinkingConfigurationError = thinkingError;
    }

    public static GeminiApiException FromResponse(HttpResponseMessage response, string json, DateTimeOffset? now = null)
    {
        var retryAfter = response.Headers.RetryAfter?.Delta;
        if (retryAfter == null && response.Headers.RetryAfter?.Date is { } retryDate)
            retryAfter = retryDate - (now ?? DateTimeOffset.UtcNow);
        bool dailyQuota = false, thinkingError = false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                // The HTTP status drives retry policy. Detail is inspected only for specific configuration fields.
                if (response.StatusCode == HttpStatusCode.BadRequest && error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                {
                    var detail = message.GetString() ?? string.Empty;
                    thinkingError = ThinkingFields
                        .Any(field => detail.Contains(field, StringComparison.OrdinalIgnoreCase));
                }
                if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                {
                    foreach (var detail in details.EnumerateArray())
                    {
                        if (detail.ValueKind != JsonValueKind.Object) continue;
                        if (detail.TryGetProperty("retryDelay", out var delay) && delay.ValueKind == JsonValueKind.String &&
                            double.TryParse(delay.GetString()?.TrimEnd('s'), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) &&
                            double.IsFinite(seconds) && seconds > 0 && seconds <= TimeSpan.FromDays(365).TotalSeconds)
                        {
                            var duration = TimeSpan.FromSeconds(seconds);
                            if (retryAfter == null || duration > retryAfter) retryAfter = duration;
                        }
                        if (detail.TryGetProperty("violations", out var violations) && violations.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var violation in violations.EnumerateArray())
                            {
                                if (violation.ValueKind != JsonValueKind.Object) continue;
                                var metric = violation.TryGetProperty("quotaMetric", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
                                dailyQuota |= metric.Contains("per_day", StringComparison.OrdinalIgnoreCase) ||
                                              metric.Contains("PerDay", StringComparison.OrdinalIgnoreCase);
                            }
                        }
                    }
                }
            }
        }
        catch (JsonException) { }
        if (retryAfter <= TimeSpan.Zero) retryAfter = null;
        return new GeminiApiException(response.StatusCode, retryAfter, dailyQuota, thinkingError);
    }
}
