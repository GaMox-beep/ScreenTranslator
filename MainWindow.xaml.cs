using System.Windows;
using ScreenTranslator.Services.Capture;
using ScreenTranslator.Services.Hotkey;
using ScreenTranslator.Services.Ocr;
using ScreenTranslator.ViewModels;
using ScreenTranslator.Views;

namespace ScreenTranslator;

public partial class MainWindow : Window
{
    private readonly GlobalHotkeyManager _hotkeyManager = new();
    private readonly WindowsMediaOcr _ocrEngine = new();
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        TxtApiKey.Password = ViewModel.ApiKey;

        Loaded += (_, _) => RegisterCurrentHotkey();
        Closing += (_, _) => _hotkeyManager.Unregister();
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
    }

    private void RegisterCurrentHotkey()
    {
        var success = _hotkeyManager.Register(this, ViewModel.SelectedModifier, ViewModel.SelectedKey);
        if (success)
        {
            ViewModel.SetStatus($"● Đang hoạt động (Phím tắt: {ViewModel.SelectedModifier} + {ViewModel.SelectedKey})", isError: false);
        }
        else
        {
            ViewModel.SetStatus($"⚠️ Không thể đăng ký phím tắt {ViewModel.SelectedModifier} + {ViewModel.SelectedKey} (bị trùng)", isError: true);
        }
    }

    private void OnHotkeyPressed()
    {
        SnippingOverlay.StartSnipping(async (area, dpiScaleX, dpiScaleY) =>
        {
            try
            {
                ViewModel.SetStatus("⏳ Đang nhận diện chữ...", isError: false);

                // 1. Chụp ảnh vùng chọn hoàn toàn trong bộ nhớ RAM
                using var bitmap = ScreenCaptureService.CaptureRegion(area, dpiScaleX, dpiScaleY);

                // 2. Nhận diện chữ bằng Windows Media OCR (Offline, 0MB phụ thuộc)
                var recognizedText = await _ocrEngine.RecognizeTextAsync(bitmap, ViewModel.SelectedSourceLanguage);

                // 3. Nếu không có chữ: dừng lại ngay (tiết kiệm token)
                if (string.IsNullOrWhiteSpace(recognizedText))
                {
                    ViewModel.SetStatus("⚠️ Không tìm thấy văn bản nào trong vùng chọn.", isError: false);
                    return;
                }

                // 4. Hiển thị chữ đã nhận diện thành công
                var previewText = recognizedText.Replace(Environment.NewLine, " ");
                if (previewText.Length > 60)
                {
                    previewText = previewText[..57] + "...";
                }

                ViewModel.SetStatus($"🔍 Đã nhận diện: \"{previewText}\"", isError: false);
            }
            catch (Exception ex)
            {
                ViewModel.SetStatus($"❌ Lỗi nhận diện: {ex.Message}", isError: true);
            }
        });
    }

    private void TxtApiKey_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.ApiKey = TxtApiKey.Password;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveSettings();
        RegisterCurrentHotkey();
    }

    private void BtnTestCapture_Click(object sender, RoutedEventArgs e)
    {
        OnHotkeyPressed();
    }
}
