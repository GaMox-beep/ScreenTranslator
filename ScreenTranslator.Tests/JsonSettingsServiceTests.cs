using System.IO;
using ScreenTranslator.Models;
using ScreenTranslator.Services.Storage;

namespace ScreenTranslator.Tests;

public sealed class JsonSettingsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ScreenTranslator.Tests", Guid.NewGuid().ToString("N"));

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
