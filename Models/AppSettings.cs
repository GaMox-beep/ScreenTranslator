namespace ScreenTranslator.Models;

public class AppSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = "Auto Detect";
    public string TargetLanguage { get; set; } = "Vietnamese";
    public string HotkeyModifier { get; set; } = "Alt";
    public string HotkeyKey { get; set; } = "Q";
    public string LastWorkingModel { get; set; } = string.Empty;
}
