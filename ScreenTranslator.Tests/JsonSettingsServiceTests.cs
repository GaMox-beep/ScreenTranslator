using System.IO;
using System.Text.Json;
using ScreenTranslator.Models;
using ScreenTranslator.Services.Storage;

namespace ScreenTranslator.Tests;

public sealed class JsonSettingsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ScreenTranslator.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Save_EncryptsApiKeyAndRoundTripsAcrossServiceInstances()
    {
        var service = new JsonSettingsService(_directory);
        Assert.True(service.Save(new AppSettings { ApiKey = "secret-test-key" }));
        var json = File.ReadAllText(Path.Combine(_directory, "settings.json"));
        Assert.DoesNotContain("secret-test-key", json);
        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("ApiKey", out _));
        Assert.NotEmpty(doc.RootElement.GetProperty("ProtectedApiKey").GetString()!);
        Assert.Equal("secret-test-key", new JsonSettingsService(_directory).Load().ApiKey);
    }

    [Fact]
    public void Load_MigratesLegacyPlaintextWithoutLosingPreferences()
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "settings.json");
        File.WriteAllText(file, JsonSerializer.Serialize(new AppSettings
        {
            ApiKey = "legacy-test-key", SourceLanguage = "Japanese", HotkeyKey = "Z", LastWorkingModel = "models/working"
        }));
        var service = new JsonSettingsService(_directory);
        var loaded = service.Load();
        Assert.Equal("legacy-test-key", loaded.ApiKey);
        Assert.Equal("Japanese", loaded.SourceLanguage);
        Assert.Equal("Z", loaded.HotkeyKey);
        Assert.Equal("models/working", loaded.LastWorkingModel);
        Assert.DoesNotContain("legacy-test-key", File.ReadAllText(file));
        Assert.Equal("legacy-test-key", new JsonSettingsService(_directory).Load().ApiKey);
        Assert.Null(service.LastError);
    }

    [Fact]
    public void Load_WithInvalidEncryptedKey_ReportsErrorAndAllowsExplicitReplacement()
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "settings.json");
        const string corrupt = """{"ProtectedApiKey":"invalid base64!","SourceLanguage":"Japanese","LastWorkingModel":"models/working"}""";
        File.WriteAllText(file, corrupt);
        var service = new JsonSettingsService(_directory);
        var loaded = service.Load();
        Assert.Empty(loaded.ApiKey);
        Assert.Equal("Japanese", loaded.SourceLanguage);
        Assert.Contains("Không thể giải mã", service.LastError);
        service.SaveLastWorkingModel("models/new");
        Assert.Equal(corrupt, File.ReadAllText(file));
        Assert.True(service.Save(new AppSettings { ApiKey = "replacement-test-key" }));
        var restored = service.Load();
        Assert.Equal("replacement-test-key", restored.ApiKey);
        Assert.Equal("models/working", restored.LastWorkingModel);
    }

    [Fact]
    public void Save_WithStalePreferences_PreservesLatestWorkingModel()
    {
        var preferences = new JsonSettingsService(_directory);
        var translator = new JsonSettingsService(_directory);
        Assert.True(preferences.Save(new AppSettings { ApiKey = "initial-key" }));
        var staleSettings = preferences.Load();
        translator.SaveLastWorkingModel("models/new-working-model");
        staleSettings.ApiKey = "updated-key";
        Assert.True(preferences.Save(staleSettings));

        var saved = translator.Load();
        Assert.Equal("updated-key", saved.ApiKey);
        Assert.Equal("models/new-working-model", saved.LastWorkingModel);
    }

    [Fact]
    public void SaveLastWorkingModel_WithCorruptSettings_DoesNotReplacePreferencesWithDefaults()
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "settings.json");
        File.WriteAllText(file, "{invalid json");
        var service = new JsonSettingsService(_directory);
        service.SaveLastWorkingModel("models/new-model");
        Assert.Equal("{invalid json", File.ReadAllText(file));
        Assert.NotNull(service.LastError);
    }

    [Fact]
    public void Save_WithCorruptSettings_RepairsFileUsingExplicitPreferences()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "settings.json"), "{invalid json");
        var service = new JsonSettingsService(_directory);
        Assert.True(service.Save(new AppSettings { ApiKey = "replacement-key", SourceLanguage = "Japanese" }));
        var settings = service.Load();
        Assert.Equal("replacement-key", settings.ApiKey);
        Assert.Equal("Japanese", settings.SourceLanguage);
        Assert.Null(service.LastError);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
