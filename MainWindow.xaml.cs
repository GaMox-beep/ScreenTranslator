using System.Windows;
using ScreenTranslator.Services.Hotkey;
using ScreenTranslator.ViewModels;
using ScreenTranslator.Views;

namespace ScreenTranslator;

public partial class MainWindow : Window
{
    private readonly GlobalHotkeyManager _hotkeyManager = new();
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
        // Khi bấm phím tắt ở bất cứ đâu trong Windows, mở màn hình quét
        SnippingOverlay.StartSnipping(area =>
        {
            ViewModel.SetStatus($"Đã quét vùng: {(int)area.Width}x{(int)area.Height} tại ({(int)area.X}, {(int)area.Y})", isError: false);
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
