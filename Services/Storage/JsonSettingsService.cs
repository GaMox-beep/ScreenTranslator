using System.IO;
using System.Text.Json;
using ScreenTranslator.Models;

namespace ScreenTranslator.Services.Storage;

public class JsonSettingsService : ISettingsService
{
    private readonly string _settingsDir;
    private readonly string _settingsFile;

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    public string? LastError { get; private set; }

    public JsonSettingsService(string? customSettingsDir = null)
    {
        _settingsDir = customSettingsDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ScreenTranslator");
        _settingsFile = Path.Combine(_settingsDir, "settings.json");
    }

    public AppSettings Load()
    {
        LastError = null;
        try
        {
            if (File.Exists(_settingsFile))
            {
                var json = File.ReadAllText(_settingsFile);
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

    public bool Save(AppSettings settings)
    {
        LastError = null;
        try
        {
            if (!Directory.Exists(_settingsDir))
            {
                Directory.CreateDirectory(_settingsDir);
            }

            var json = JsonSerializer.Serialize(settings, WriteOptions);
            File.WriteAllText(_settingsFile, json);
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Lỗi lưu file cấu hình: {ex.Message}";
            return false;
        }
    }

    public void SaveLastWorkingModel(string model)
    {
        try
        {
            var settings = Load();
            if (settings.LastWorkingModel != model)
            {
                settings.LastWorkingModel = model;
                Save(settings);
            }
        }
        catch
        {
            // Bỏ qua lỗi phụ nếu không ghi được model
        }
    }
}
