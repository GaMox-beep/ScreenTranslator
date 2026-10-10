using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using ScreenTranslator.Models;

namespace ScreenTranslator.Services.Storage;

public class JsonSettingsService : ISettingsService
{
    private readonly string _settingsDir;
    private readonly string _settingsFile;
    private readonly Lock _fileLock;
    private static readonly ConcurrentDictionary<string, Lock> FileLocks = new(StringComparer.OrdinalIgnoreCase);

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
        _settingsDir = Path.GetFullPath(customSettingsDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ScreenTranslator"));
        _settingsFile = Path.Combine(_settingsDir, "settings.json");
        _fileLock = FileLocks.GetOrAdd(_settingsFile, _ => new Lock());
    }

    public AppSettings Load()
    {
        lock (_fileLock)
        {
            LastError = null;
            try
            {
                return ReadSettings();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                LastError = $"Lỗi đọc file cấu hình: {ex.Message}";
                return new AppSettings();
            }
        }
    }

    public bool Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_fileLock)
        {
            LastError = null;
            try
            {
                // Model is maintained by the translator; the settings window may hold an older snapshot.
                var currentModel = settings.LastWorkingModel;
                if (File.Exists(_settingsFile))
                {
                    try
                    {
                        currentModel = ReadSettings().LastWorkingModel;
                    }
                    catch (JsonException)
                    {
                        // An explicit preferences save can repair a corrupt file.
                        currentModel = string.Empty;
                    }
                }
                var merged = new AppSettings
                {
                    ApiKey = settings.ApiKey,
                    SourceLanguage = settings.SourceLanguage,
                    TargetLanguage = settings.TargetLanguage,
                    HotkeyModifier = settings.HotkeyModifier,
                    HotkeyKey = settings.HotkeyKey,
                    LastWorkingModel = currentModel
                };
                WriteSettings(merged);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                LastError = $"Lỗi lưu file cấu hình: {ex.Message}";
                return false;
            }
        }
    }

    public void SaveLastWorkingModel(string model)
    {
        lock (_fileLock)
        {
            LastError = null;
            try
            {
                var settings = ReadSettings();
                if (settings.LastWorkingModel != model)
                {
                    settings.LastWorkingModel = model;
                    WriteSettings(settings);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                LastError = $"Lỗi lưu model: {ex.Message}";
            }
        }
    }

    private AppSettings ReadSettings()
    {
        if (!File.Exists(_settingsFile)) return new AppSettings();
        return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsFile), ReadOptions)
            ?? throw new JsonException("File cấu hình không chứa dữ liệu hợp lệ.");
    }

    private void WriteSettings(AppSettings settings)
    {
        Directory.CreateDirectory(_settingsDir);
        var temporaryFile = Path.Combine(_settingsDir, $"settings.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryFile, JsonSerializer.Serialize(settings, WriteOptions));
            File.Move(temporaryFile, _settingsFile, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryFile)) File.Delete(temporaryFile);
        }
    }
}
