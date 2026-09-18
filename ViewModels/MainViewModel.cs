using ScreenTranslator.Models;
using ScreenTranslator.Services.Storage;

namespace ScreenTranslator.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _settings;
    private string _apiKey = string.Empty;
    private string _selectedSourceLanguage = "Auto Detect";
    private string _selectedTargetLanguage = "Vietnamese";
    private string _selectedModifier = "Alt";
    private string _selectedKey = "Q";
    private string _statusText = "Sẵn sàng";
    private bool _isStatusError;

    public IReadOnlyList<string> SourceLanguages => LanguagePresets.SourceLanguages;
    public IReadOnlyList<string> TargetLanguages => LanguagePresets.TargetLanguages;
    public IReadOnlyList<string> Modifiers => HotkeyPresets.Modifiers;
    public IReadOnlyList<string> Keys => HotkeyPresets.Keys;

    public string ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    public string SelectedSourceLanguage
    {
        get => _selectedSourceLanguage;
        set => SetProperty(ref _selectedSourceLanguage, value);
    }

    public string SelectedTargetLanguage
    {
        get => _selectedTargetLanguage;
        set => SetProperty(ref _selectedTargetLanguage, value);
    }

    public string SelectedModifier
    {
        get => _selectedModifier;
        set => SetProperty(ref _selectedModifier, value);
    }

    public string SelectedKey
    {
        get => _selectedKey;
        set => SetProperty(ref _selectedKey, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsStatusError
    {
        get => _isStatusError;
        set => SetProperty(ref _isStatusError, value);
    }

    public MainViewModel() : this(new JsonSettingsService())
    {
    }

    public MainViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _settings = _settingsService.Load();
        ApplySettingsToViewModel();

        if (!string.IsNullOrEmpty(_settingsService.LastError))
        {
            SetStatus(_settingsService.LastError, isError: true);
        }
    }

    private void ApplySettingsToViewModel()
    {
        ApiKey = _settings.ApiKey;

        SelectedSourceLanguage = SourceLanguages.Contains(_settings.SourceLanguage)
            ? _settings.SourceLanguage
            : "Auto Detect";

        SelectedTargetLanguage = TargetLanguages.Contains(_settings.TargetLanguage)
            ? _settings.TargetLanguage
            : "Vietnamese";

        SelectedModifier = Modifiers.Contains(_settings.HotkeyModifier)
            ? _settings.HotkeyModifier
            : "Alt";

        SelectedKey = Keys.Contains(_settings.HotkeyKey)
            ? _settings.HotkeyKey
            : "Q";
    }

    public void SaveSettings()
    {
        _settings.ApiKey = ApiKey.Trim();
        _settings.SourceLanguage = SelectedSourceLanguage;
        _settings.TargetLanguage = SelectedTargetLanguage;
        _settings.HotkeyModifier = SelectedModifier;
        _settings.HotkeyKey = SelectedKey;

        var success = _settingsService.Save(_settings);
        if (success)
        {
            SetStatus("Đã lưu cài đặt thành công!", isError: false);
        }
        else
        {
            SetStatus(_settingsService.LastError ?? "Lỗi lưu cấu hình!", isError: true);
        }
    }

    public void SetStatus(string message, bool isError = false)
    {
        StatusText = message;
        IsStatusError = isError;
    }
}
