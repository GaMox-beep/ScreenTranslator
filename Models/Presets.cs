namespace ScreenTranslator.Models;

public static class LanguagePresets
{
    public static readonly IReadOnlyList<string> SourceLanguages = new[]
    {
        "Auto Detect",
        "English",
        "Japanese",
        "Chinese",
        "Korean",
        "French",
        "German",
        "Spanish",
        "Russian"
    };

    public static readonly IReadOnlyList<string> TargetLanguages = new[]
    {
        "Vietnamese",
        "English",
        "Japanese",
        "Chinese",
        "Korean",
        "French"
    };
}

public static class HotkeyPresets
{
    public static readonly IReadOnlyList<string> Modifiers = new[]
    {
        "Alt",
        "Ctrl",
        "Shift",
        "Ctrl + Alt",
        "Ctrl + Shift",
        "Alt + Shift"
    };

    public static readonly IReadOnlyList<string> Keys = new[]
    {
        "Q", "S", "T", "D", "F", "Z", "X", "C",
        "1", "2", "3", "` (Tilde)"
    };
}
