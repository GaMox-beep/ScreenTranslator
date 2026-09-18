using ScreenTranslator.Models;

namespace ScreenTranslator.Services.Storage;

public static class SettingsManager
{
    private static readonly JsonSettingsService Service = new();

    public static string? LastError => Service.LastError;

    public static AppSettings Load() => Service.Load();

    public static bool Save(AppSettings settings) => Service.Save(settings);

    public static void SaveLastWorkingModel(string model) => Service.SaveLastWorkingModel(model);
}
