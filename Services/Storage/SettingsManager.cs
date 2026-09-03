using System.IO;
using System.Text.Json;
using ScreenTranslator.Models;

namespace ScreenTranslator.Services.Storage;

public class SettingsManager
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "ScreenTranslator");
        
    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    public static string? LastError { get; private set; }

    public static AppSettings Load()
    {
        LastError = null;
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
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

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(settings, options);
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
