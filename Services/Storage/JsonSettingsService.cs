using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScreenTranslator.Models;

namespace ScreenTranslator.Services.Storage;

public class JsonSettingsService : ISettingsService
{
    private readonly string _settingsDir;
    private readonly string _settingsFile;
    private readonly Lock _fileLock;
    private static readonly ConcurrentDictionary<string, Lock> FileLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly byte[] KeyEntropy = Encoding.UTF8.GetBytes("ScreenTranslator.ApiKey.v1");

    private sealed class StoredSettings
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("ApiKey")]
        public string? LegacyApiKey { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ProtectedApiKey { get; set; }
        public string SourceLanguage { get; set; } = "Windows Default";
        public string TargetLanguage { get; set; } = "Vietnamese";
        public string HotkeyModifier { get; set; } = "Alt";
        public string HotkeyKey { get; set; } = "Q";
        public string LastWorkingModel { get; set; } = string.Empty;
    }

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
                var settings = ReadSettings(out var needsMigration, allowKeyFailure: true);
                if (needsMigration)
                {
                    try { WriteSettings(settings); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
                    {
                        LastError = "Đã đọc cấu hình nhưng chưa mã hóa được API key. Vui lòng lưu lại cài đặt.";
                    }
                }
                return settings;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or CryptographicException)
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
                        currentModel = ReadStoredSettings().LastWorkingModel;
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
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or CryptographicException)
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
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or CryptographicException)
            {
                LastError = $"Lỗi lưu model: {ex.Message}";
            }
        }
    }

    private StoredSettings ReadStoredSettings()
    {
        if (!File.Exists(_settingsFile)) return new StoredSettings();
        return JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(_settingsFile), ReadOptions)
            ?? throw new JsonException("File cấu hình không chứa dữ liệu hợp lệ.");
    }

    private AppSettings ReadSettings() => ReadSettings(out _);

    private AppSettings ReadSettings(out bool needsMigration, bool allowKeyFailure = false)
    {
        var stored = ReadStoredSettings();
        needsMigration = stored.LegacyApiKey != null;
        var key = stored.LegacyApiKey ?? string.Empty;
        if (stored.ProtectedApiKey != null)
        {
            try { key = UnprotectKey(stored.ProtectedApiKey); }
            catch (CryptographicException ex) when (allowKeyFailure)
            {
                LastError = ex.Message;
                key = string.Empty;
                needsMigration = false;
            }
        }
        return new AppSettings
        {
            ApiKey = key,
            SourceLanguage = stored.SourceLanguage,
            TargetLanguage = stored.TargetLanguage,
            HotkeyModifier = stored.HotkeyModifier,
            HotkeyKey = stored.HotkeyKey,
            LastWorkingModel = stored.LastWorkingModel
        };
    }

    private static string ProtectKey(string key)
    {
        var bytes = Encoding.UTF8.GetBytes(key);
        try { return Convert.ToBase64String(ProtectedData.Protect(bytes, KeyEntropy, DataProtectionScope.CurrentUser)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static string UnprotectKey(string encryptedKey)
    {
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(encryptedKey), KeyEntropy, DataProtectionScope.CurrentUser);
            try { return Encoding.UTF8.GetString(bytes); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            throw new CryptographicException("Không thể giải mã API key cho tài khoản Windows này. Vui lòng nhập và lưu lại API key.", ex);
        }
    }

    private void WriteSettings(AppSettings settings)
    {
        Directory.CreateDirectory(_settingsDir);
        var temporaryFile = Path.Combine(_settingsDir, $"settings.{Guid.NewGuid():N}.tmp");
        try
        {
            var stored = new StoredSettings
            {
                ProtectedApiKey = string.IsNullOrEmpty(settings.ApiKey) ? null : ProtectKey(settings.ApiKey),
                SourceLanguage = settings.SourceLanguage,
                TargetLanguage = settings.TargetLanguage,
                HotkeyModifier = settings.HotkeyModifier,
                HotkeyKey = settings.HotkeyKey,
                LastWorkingModel = settings.LastWorkingModel
            };
            File.WriteAllText(temporaryFile, JsonSerializer.Serialize(stored, WriteOptions));
            File.Move(temporaryFile, _settingsFile, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryFile)) File.Delete(temporaryFile);
        }
    }
}
