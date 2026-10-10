namespace ScreenTranslator.ViewModels;

public class ResultViewModel : ViewModelBase
{
    private string _displayText = "Đang dịch...";
    private bool _isLoading = true;
    private bool _isError;
    private string _copyButtonText = "Sao chép";
    private string _translatedContent = string.Empty;
    private string _originalText = string.Empty;
    private string _languagePair = string.Empty;
    private string _elapsedText = string.Empty;

    public string OriginalText
    {
        get => _originalText;
        set { if (SetProperty(ref _originalText, value)) OnPropertyChanged(nameof(HasOriginalText)); }
    }

    public bool HasOriginalText => !string.IsNullOrWhiteSpace(OriginalText);
    public string LanguagePair { get => _languagePair; set => SetProperty(ref _languagePair, value); }
    public string ElapsedText { get => _elapsedText; set => SetProperty(ref _elapsedText, value); }

    public string DisplayText
    {
        get => _displayText;
        set => SetProperty(ref _displayText, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set { if (SetProperty(ref _isLoading, value)) OnPropertyChanged(nameof(CanCopy)); }
    }

    public bool IsError
    {
        get => _isError;
        set { if (SetProperty(ref _isError, value)) OnPropertyChanged(nameof(CanCopy)); }
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
        OriginalText = string.Empty;
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

    public void SetCancelled() => SetMessage("Đã hủy tác vụ.");
    public void SetNoText() => SetMessage("Không tìm thấy văn bản nào trong vùng chọn.");

    private void SetMessage(string message)
    {
        IsLoading = false;
        IsError = false;
        TranslatedContent = string.Empty;
        DisplayText = message;
    }
}
