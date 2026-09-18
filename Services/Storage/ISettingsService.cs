using ScreenTranslator.Models;

namespace ScreenTranslator.Services.Storage;

public interface ISettingsService
{
    AppSettings Load();
    bool Save(AppSettings settings);
    void SaveLastWorkingModel(string model);
    string? LastError { get; }
}
