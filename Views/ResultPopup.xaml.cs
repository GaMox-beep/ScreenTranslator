using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ScreenTranslator.Services.Capture;
using ScreenTranslator.ViewModels;

namespace ScreenTranslator.Views;

public partial class ResultPopup : Window
{
    private static ResultPopup? _currentPopup;
    private readonly Stopwatch _elapsed = new();
    private readonly DispatcherTimer _progressTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private Action? _cancelAction;
    public ResultViewModel ViewModel => (ResultViewModel)DataContext;

    public ResultPopup() : this(null) { }

    internal ResultPopup(Action? cancel)
    {
        _cancelAction = cancel;
        InitializeComponent();
        _progressTimer.Tick += (_, _) => UpdateElapsed();

        // Cửa sổ sẽ ở nguyên trên màn hình, chỉ đóng khi click "Đóng" hoặc ấn Escape
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };

        Closed += (_, _) =>
        {
            var cancel = ViewModel.IsLoading ? _cancelAction : null;
            StopProgress();
            cancel?.Invoke();
            if (_currentPopup == this)
            {
                _currentPopup = null;
            }
        };
    }

    public static void CloseCurrent() => _currentPopup?.Close();

    public static ResultPopup ShowLoading(Point screenPosition, string sourceLanguage, string targetLanguage, Action cancel)
    {
        CloseCurrent();

        var popup = new ResultPopup(cancel);
        _currentPopup = popup;
        popup.ViewModel.LanguagePair = $"{(sourceLanguage == "Windows Default" ? "Nguồn theo Windows" : sourceLanguage)} → {targetLanguage}";
        popup.ViewModel.SetLoading("Đang nhận diện chữ...");
        popup._elapsed.Start();
        popup.UpdateElapsed();
        popup._progressTimer.Start();

        popup.Loaded += (_, _) => ScreenCoordinates.PlaceNear(popup, screenPosition);
        popup.Show();
        popup.Activate();
        return popup;
    }

    public void UpdateSuccess(string translatedText)
    {
        Dispatcher.Invoke(() =>
        {
            ViewModel.SetSuccess(translatedText);
            StopProgress();
        });
    }

    public void UpdateError(string errorMessage)
    {
        Dispatcher.Invoke(() =>
        {
            ViewModel.SetError(errorMessage);
            StopProgress();
        });
    }

    public void UpdateProgress(string message) => Dispatcher.Invoke(() => ViewModel.DisplayText = message);
    public void UpdateOriginalText(string text) => Dispatcher.Invoke(() => ViewModel.OriginalText = text);
    public void UpdateCancelled() => Dispatcher.Invoke(() => { ViewModel.SetCancelled(); StopProgress(); });
    public void UpdateNoText() => Dispatcher.Invoke(() => { ViewModel.SetNoText(); StopProgress(); });

    private void UpdateElapsed() => ViewModel.ElapsedText = $"{_elapsed.Elapsed.TotalSeconds:0.0} giây";

    private void StopProgress()
    {
        _progressTimer.Stop();
        _elapsed.Stop();
        UpdateElapsed();
        _cancelAction = null;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        var cancel = _cancelAction;
        UpdateCancelled();
        cancel?.Invoke();
    }

    private void BtnCopyOriginal_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasOriginalText) return;
        try
        {
            Clipboard.SetDataObject(ViewModel.OriginalText, true);
            BtnCopyOriginal.Content = "Đã chép";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            BtnCopyOriginal.Content = "Thử lại";
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(ViewModel.TranslatedContent))
        {
            try
            {
                Clipboard.SetText(ViewModel.TranslatedContent);
                ViewModel.CopyButtonText = "Đã chép";
            }
            catch
            {
                try
                {
                    Clipboard.SetDataObject(ViewModel.TranslatedContent, true);
                    ViewModel.CopyButtonText = "Đã chép";
                }
                catch
                {
                    // Tránh crash nếu clipboard bị tiến trình khác chiếm dụng
                }
            }
        }
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TxtResult_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ContentScrollViewer.ScrollToVerticalOffset(ContentScrollViewer.VerticalOffset - (e.Delta / 3.0));
        e.Handled = true;
    }
}
