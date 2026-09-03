using System.Collections.ObjectModel;
using ScreenTranslator.Models;
using ScreenTranslator.Services.Storage;
using ScreenTranslator.Services.Translation;

namespace ScreenTranslator.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private ProviderInfo? _selectedProvider;
    private string _customBaseUrl = string.Empty;
    private bool _isCustomUrlVisible;
    private string _apiKey = string.Empty;
    private string _selectedModel = string.Empty;
    private string _selectedSourceLanguage = "Auto Detect";
    private string _selectedTargetLanguage = "Vietnamese";
    private string _selectedModifier = "Alt";
    private string _selectedKey = "Q";
    private string _statusText = "Sẵn sàng";
    private bool _isStatusError;

    public ObservableCollection<ProviderInfo> Providers { get; } = new();
    public ObservableCollection<string> AvailableModels { get; } = new();
    public IReadOnlyList<string> SourceLanguages => LanguagePresets.SourceLanguages;
    public IReadOnlyList<string> TargetLanguages => LanguagePresets.TargetLanguages;
    public IReadOnlyList<string> Modifiers => HotkeyPresets.Modifiers;
    public IReadOnlyList<string> Keys => HotkeyPresets.Keys;

    public ProviderInfo? SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (SetProperty(ref _selectedProvider, value) && value != null)
            {
                IsCustomUrlVisible = value.RequiresCustomUrl;
                UpdateAvailableModels(value);
            }
        }
    }

    public string CustomBaseUrl
    {
        get => _customBaseUrl;
        set => SetProperty(ref _customBaseUrl, value);
    }

    public bool IsCustomUrlVisible
    {
        get => _isCustomUrlVisible;
        set => SetProperty(ref _isCustomUrlVisible, value);
    }

    public string ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    public string SelectedModel
    {
        get => _selectedModel;
        set => SetProperty(ref _selectedModel, value);
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

    public MainViewModel()
    {
        _settings = SettingsManager.Load();
        LoadProviders();
        ApplySettingsToViewModel();

        // Kiểm tra xem lúc load file có lỗi không
        if (!string.IsNullOrEmpty(ProviderManager.LastError))
        {
            SetStatus(ProviderManager.LastError, isError: true);
        }
        else if (!string.IsNullOrEmpty(SettingsManager.LastError))
        {
            SetStatus(SettingsManager.LastError, isError: true);
        }
    }

    private void LoadProviders()
    {
        Providers.Clear();
        var list = ProviderManager.GetProviders();
        foreach (var provider in list)
        {
            Providers.Add(provider);
        }
    }

    private void ApplySettingsToViewModel()
    {
        ApiKey = _settings.ApiKey;
        CustomBaseUrl = _settings.CustomBaseUrl;

        // Chọn provider
        var matchedProvider = Providers.FirstOrDefault(p =>
            p.Name.Equals(_settings.Provider, StringComparison.OrdinalIgnoreCase) ||
            p.Id.Equals(_settings.Provider, StringComparison.OrdinalIgnoreCase)) ?? Providers.FirstOrDefault();

        SelectedProvider = matchedProvider;

        // Model
        SelectedModel = string.IsNullOrWhiteSpace(_settings.Model)
            ? (matchedProvider?.Models.FirstOrDefault() ?? string.Empty)
            : _settings.Model;

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

    private void UpdateAvailableModels(ProviderInfo provider)
    {
        AvailableModels.Clear();
        foreach (var m in provider.Models)
        {
            AvailableModels.Add(m);
        }

        if (!AvailableModels.Contains(SelectedModel))
        {
            SelectedModel = AvailableModels.FirstOrDefault() ?? string.Empty;
        }
    }

    public void SaveSettings()
    {
        if (SelectedProvider != null)
        {
            _settings.Provider = SelectedProvider.Name;
        }

        _settings.CustomBaseUrl = CustomBaseUrl.Trim();
        _settings.ApiKey = ApiKey.Trim();
        _settings.Model = SelectedModel.Trim();
        _settings.SourceLanguage = SelectedSourceLanguage;
        _settings.TargetLanguage = SelectedTargetLanguage;
        _settings.HotkeyModifier = SelectedModifier;
        _settings.HotkeyKey = SelectedKey;

        var success = SettingsManager.Save(_settings);
        if (success)
        {
            SetStatus("Đã lưu cài đặt thành công!", isError: false);
        }
        else
        {
            SetStatus(SettingsManager.LastError ?? "Lỗi lưu cấu hình!", isError: true);
        }
    }

    public void SetStatus(string message, bool isError = false)
    {
        StatusText = message;
        IsStatusError = isError;
    }
}
