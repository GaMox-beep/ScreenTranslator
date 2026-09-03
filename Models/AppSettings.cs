namespace ScreenTranslator.Models;

public class AppSettings
{
    public string Provider { get; set; } = "OpenRouter";
    public string ApiKey { get; set; } = string.Empty;
    public string CustomBaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = "google/gemini-2.0-flash-001";
    public string SourceLanguage { get; set; } = "Auto Detect";
    public string TargetLanguage { get; set; } = "Vietnamese";
    public string HotkeyModifier { get; set; } = "Alt";
    public string HotkeyKey { get; set; } = "Q";
}
