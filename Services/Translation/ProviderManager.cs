using System.IO;
using System.Text.Json;
using ScreenTranslator.Models;

namespace ScreenTranslator.Services.Translation;

public static class ProviderManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static List<ProviderInfo>? _cachedProviders;
    public static string? LastError { get; private set; }

    public static List<ProviderInfo> GetProviders()
    {
        if (_cachedProviders != null) return _cachedProviders;

        LastError = null;
        try
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "providers.json");
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                _cachedProviders = JsonSerializer.Deserialize<List<ProviderInfo>>(json, JsonOptions);

                if (_cachedProviders != null && _cachedProviders.Count > 0)
                {
                    return _cachedProviders;
                }
            }
            else
            {
                LastError = "Không tìm thấy file providers.json. Đang sử dụng danh sách mặc định.";
            }
        }
        catch (Exception ex)
        {
            LastError = $"Lỗi đọc file providers.json: {ex.Message}";
        }

        // Danh sách dự phòng mặc định nếu file không tồn tại hoặc bị lỗi
        _cachedProviders = GetDefaultProviders();
        return _cachedProviders;
    }

    private static List<ProviderInfo> GetDefaultProviders() => new()
    {
        new ProviderInfo
        {
            Id = "openrouter",
            Name = "OpenRouter",
            BaseUrl = "https://openrouter.ai/api/v1/",
            Models = new List<string> { "google/gemini-2.0-flash-001", "google/gemini-2.0-flash-exp:free", "deepseek/deepseek-chat" }
        },
        new ProviderInfo
        {
            Id = "gemini",
            Name = "Google Gemini",
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/",
            Models = new List<string> { "gemini-2.0-flash", "gemini-1.5-flash" }
        },
        new ProviderInfo
        {
            Id = "openai",
            Name = "OpenAI",
            BaseUrl = "https://api.openai.com/v1/",
            Models = new List<string> { "gpt-4o-mini", "gpt-4o" }
        }
    };
}
