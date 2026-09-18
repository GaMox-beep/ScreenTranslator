namespace ScreenTranslator.ViewModels;

public class ResultViewModel : ViewModelBase
{
    private string _displayText = "Đang dịch...";
    private bool _isLoading = true;
    private bool _isError;
    private string _copyButtonText = "Sao chép";
    private string _translatedContent = string.Empty;

    public string DisplayText
    {
        get => _displayText;
        set => SetProperty(ref _displayText, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsError
    {
        get => _isError;
        set => SetProperty(ref _isError, value);
    }

    public string CopyButtonText
    {
        get => _copyButtonText;
        set => SetProperty(ref _copyButtonText, value);
    }

    public string TranslatedContent
    {
        get => _translatedContent;
        set
        {
            if (SetProperty(ref _translatedContent, value))
            {
                OnPropertyChanged(nameof(CanCopy));
            }
        }
    }

    public bool CanCopy => !IsLoading && !IsError && !string.IsNullOrWhiteSpace(TranslatedContent);

    public void SetLoading(string message = "Đang dịch...")
    {
        IsLoading = true;
        IsError = false;
        DisplayText = message;
        TranslatedContent = string.Empty;
        CopyButtonText = "Sao chép";
        OnPropertyChanged(nameof(CanCopy));
    }

    public void SetSuccess(string result)
    {
        IsLoading = false;
        IsError = false;
        TranslatedContent = result;
        DisplayText = result;
        CopyButtonText = "Sao chép";
        OnPropertyChanged(nameof(CanCopy));
    }

    public void SetError(string errorMessage)
    {
        IsLoading = false;
        IsError = true;
        DisplayText = errorMessage;
        TranslatedContent = string.Empty;
        CopyButtonText = "Sao chép";
        OnPropertyChanged(nameof(CanCopy));
    }
}
