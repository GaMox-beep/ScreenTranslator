namespace ScreenTranslator.ViewModels;

public class ResultViewModel : ViewModelBase
{
    private string _originalText = string.Empty;
    private string _translatedText = string.Empty;
    private bool _isLoading;
    private string _errorMessage = string.Empty;

    public string OriginalText
    {
        get => _originalText;
        set => SetProperty(ref _originalText, value);
    }

    public string TranslatedText
    {
        get => _translatedText;
        set => SetProperty(ref _translatedText, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }
}
