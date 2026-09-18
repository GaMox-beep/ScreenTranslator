using ScreenTranslator.Models;
using ScreenTranslator.Tests.Fakes;
using ScreenTranslator.ViewModels;

namespace ScreenTranslator.Tests;

public class MainViewModelTests
{
    [Fact]
    public void Constructor_LoadsSettingsFromServiceCorrectly()
    {
        // Arrange
        var fakeStorage = new InMemorySettingsService();
        fakeStorage.Save(new AppSettings
        {
            ApiKey = "test-api-key-123",
            SourceLanguage = "Japanese",
            TargetLanguage = "Vietnamese",
            HotkeyModifier = "Ctrl + Alt",
            HotkeyKey = "Z"
        });

        // Act
        var vm = new MainViewModel(fakeStorage);

        // Assert
        Assert.Equal("test-api-key-123", vm.ApiKey);
        Assert.Equal("Japanese", vm.SelectedSourceLanguage);
        Assert.Equal("Vietnamese", vm.SelectedTargetLanguage);
        Assert.Equal("Ctrl + Alt", vm.SelectedModifier);
        Assert.Equal("Z", vm.SelectedKey);
    }

    [Fact]
    public void SaveSettings_PersistsChangesToStorageService()
    {
        // Arrange
        var fakeStorage = new InMemorySettingsService();
        var vm = new MainViewModel(fakeStorage)
        {
            ApiKey = "new-key-xyz",
            SelectedSourceLanguage = "English",
            SelectedTargetLanguage = "French",
            SelectedModifier = "Shift",
            SelectedKey = "T"
        };

        // Act
        vm.SaveSettings();

        // Assert
        var saved = fakeStorage.Load();
        Assert.Equal("new-key-xyz", saved.ApiKey);
        Assert.Equal("English", saved.SourceLanguage);
        Assert.Equal("French", saved.TargetLanguage);
        Assert.Equal("Shift", saved.HotkeyModifier);
        Assert.Equal("T", saved.HotkeyKey);
        Assert.Equal("Đã lưu cài đặt thành công!", vm.StatusText);
        Assert.False(vm.IsStatusError);
    }
}
