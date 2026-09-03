using System.IO;
using System.Text.Json;
using ScreenTranslator.Models;

namespace ScreenTranslator.Services.Storage;

public static class SettingsManager
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ScreenTranslator");

    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    public static string? LastError { get; private set; }

    public static AppSettings Load()
    {
        LastError = null;
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, ReadOptions);
                if (settings != null) return settings;
            }
        }
        catch (Exception ex)
        {
            LastError = $"Lỗi đọc file cấu hình: {ex.Message}";
        }

        return new AppSettings();
    }

    public static bool Save(AppSettings settings)
    {
        LastError = null;
        try
        {
            if (!Directory.Exists(SettingsDir))
            {
                Directory.CreateDirectory(SettingsDir);
            }

            var json = JsonSerializer.Serialize(settings, WriteOptions);
            File.WriteAllText(SettingsFile, json);
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Lỗi lưu file cấu hình: {ex.Message}";
            return false;
        }
    }
}
