using ScreenTranslator.Models;

namespace ScreenTranslator.Services.Storage;

public interface ISettingsService
{
    AppSettings Load();
    // Saves user preferences while retaining the latest model maintained by SaveLastWorkingModel.
    bool Save(AppSettings settings);
    void SaveLastWorkingModel(string model);
    string? LastError { get; }
}
