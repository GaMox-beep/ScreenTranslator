using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ScreenTranslator.Services;
using ScreenTranslator.Services.Capture;
using ScreenTranslator.Services.Hotkey;
using ScreenTranslator.Services.Ocr;
using ScreenTranslator.Services.Orchestration;
using ScreenTranslator.Services.Translation;
using ScreenTranslator.ViewModels;
using ScreenTranslator.Views;

namespace ScreenTranslator;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "WPF Windows manage resource cleanup in Closing event.")]
public partial class MainWindow : Window
{
    private readonly GlobalHotkeyManager _hotkeyManager = new();
    private readonly ITranslationOrchestrator _orchestrator;
    private CancellationTokenSource? _translationCts;
    private bool _isClosing;
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow() : this(
        App.ServiceProvider?.GetService<ITranslationOrchestrator>() ??
        new TranslationOrchestrator(
            new WindowsScreenCaptureService(),
            new WindowsMediaOcr(),
            new GoogleGeminiTranslator()))
    {
    }

    public MainWindow(ITranslationOrchestrator orchestrator)
    {
        InitializeComponent();
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        TxtApiKey.Password = ViewModel.ApiKey;

        Loaded += (_, _) => RegisterCurrentHotkey();
        Closing += (_, _) =>
        {
            _isClosing = true;
            _hotkeyManager.Dispose();
            _translationCts?.Cancel();
            ResultPopup.CloseCurrent();
        };
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;

        AppLogger.Log("[INIT] MainWindow initialized successfully.");
    }

    private void RegisterCurrentHotkey()
    {
        var success = _hotkeyManager.Register(this, ViewModel.SelectedModifier, ViewModel.SelectedKey);
        if (success)
        {
            if (!ViewModel.IsStatusError)
                ViewModel.SetStatus($"● Đang hoạt động (Phím tắt: {ViewModel.SelectedModifier} + {ViewModel.SelectedKey})", isError: false);
            AppLogger.Log($"[HOTKEY] Global hotkey registered successfully: Modifier={ViewModel.SelectedModifier}, Key={ViewModel.SelectedKey}");
        }
        else
        {
            ViewModel.SetStatus($"⚠️ Không thể đăng ký phím tắt {ViewModel.SelectedModifier} + {ViewModel.SelectedKey} (bị trùng)", isError: true);
            AppLogger.Log($"[HOTKEY] ERROR: Failed to register global hotkey: Modifier={ViewModel.SelectedModifier}, Key={ViewModel.SelectedKey}");
        }
    }

    private void OnHotkeyPressed()
    {
        AppLogger.Log("[TRIGGER] Screen snip action initiated by user hotkey.");
        SnippingOverlay.StartSnipping(async area =>
        {
            _translationCts?.Cancel();
            ResultPopup.CloseCurrent();
            using var requestCts = new CancellationTokenSource();
            _translationCts = requestCts;
            var cancellationToken = requestCts.Token;
            bool OwnsRequest() => !_isClosing && ReferenceEquals(_translationCts, requestCts);
            bool IsCurrentRequest() => !cancellationToken.IsCancellationRequested && OwnsRequest();

            ResultPopup? popup = null;

            var request = new TranslationPipelineRequest(
                area,
                ViewModel.SelectedSourceLanguage,
                ViewModel.SelectedTargetLanguage,
                ViewModel.ApiKey);

            var callbacks = new TranslationPipelineCallbacks(
                OnLoading: () =>
                {
                    if (!IsCurrentRequest()) return;
                    var popupPosition = new Point(area.X, area.Bottom + 6);
                    popup = ResultPopup.ShowLoading(popupPosition, request.SourceLanguage, request.TargetLanguage, requestCts.Cancel);
                },
                OnSuccess: text => { if (IsCurrentRequest()) popup?.UpdateSuccess(text); },
                OnError: err =>
                {
                    if (!IsCurrentRequest()) return;
                    popup ??= ResultPopup.ShowLoading(new Point(area.X, area.Bottom + 6), request.SourceLanguage, request.TargetLanguage, requestCts.Cancel);
                    popup.UpdateError(err);
                },
                OnStatusChanged: (status, isError) => { if (IsCurrentRequest()) ViewModel.SetStatus(status, isError); },
                OnProgress: status => { if (IsCurrentRequest()) popup?.UpdateProgress(status); },
                OnOriginalText: text => { if (IsCurrentRequest()) popup?.UpdateOriginalText(text); },
                OnCancelled: () =>
                {
                    if (!OwnsRequest()) return;
                    if (popup?.IsLoaded == true) popup.UpdateCancelled();
                    ViewModel.SetStatus("Đã hủy tác vụ.");
                },
                OnNoText: () => { if (IsCurrentRequest()) popup?.UpdateNoText(); });

            try
            {
                await _orchestrator.ProcessAsync(request, callbacks, cancellationToken);
            }
            finally
            {
                if (cancellationToken.IsCancellationRequested && !OwnsRequest() && popup?.IsLoaded == true) popup.Close();
                if (ReferenceEquals(_translationCts, requestCts)) _translationCts = null;
            }
        });
    }

    private void TxtApiKey_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.ApiKey = TxtApiKey.Password;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.SaveSettings()) return;
        RegisterCurrentHotkey();
        AppLogger.Log("[SETTINGS] User configuration saved successfully.");
    }

    private void BtnTestCapture_Click(object sender, RoutedEventArgs e)
    {
        OnHotkeyPressed();
    }
}
