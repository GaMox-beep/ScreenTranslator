using System.Windows;
using System.Windows.Input;
using ScreenTranslator.ViewModels;

namespace ScreenTranslator.Views;

public partial class ResultPopup : Window
{
    private static ResultPopup? _currentPopup;
    public ResultViewModel ViewModel => (ResultViewModel)DataContext;

    public ResultPopup()
    {
        InitializeComponent();

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
            if (_currentPopup == this)
            {
                _currentPopup = null;
            }
        };
    }

    public static ResultPopup ShowLoading(Point screenPosition)
    {
        if (_currentPopup != null)
        {
            try { _currentPopup.Close(); } catch { }
            _currentPopup = null;
        }

        var popup = new ResultPopup();
        _currentPopup = popup;
        popup.ViewModel.SetLoading("Đang dịch...");

        // Định vị popup gần vị trí con trỏ chuột, cách một khoảng nhẹ
        var targetX = screenPosition.X + 10;
        var targetY = screenPosition.Y + 15;

        // Giới hạn trong vùng màn hình
        var maxRight = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 780;
        var maxBottom = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 580;

        popup.Left = Math.Max(SystemParameters.VirtualScreenLeft + 10, Math.Min(targetX, maxRight));
        popup.Top = Math.Max(SystemParameters.VirtualScreenTop + 10, Math.Min(targetY, maxBottom));

        popup.Show();
        popup.Activate();
        return popup;
    }

    public void UpdateSuccess(string translatedText)
    {
        Dispatcher.Invoke(() =>
        {
            ViewModel.SetSuccess(translatedText);
        });
    }

    public void UpdateError(string errorMessage)
    {
        Dispatcher.Invoke(() =>
        {
            ViewModel.SetError(errorMessage);
        });
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
