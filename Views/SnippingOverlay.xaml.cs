using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ScreenTranslator.Services.Capture;

namespace ScreenTranslator.Views;

public partial class SnippingOverlay : Window
{
    private Point _startPoint;
    private Point _startScreenPoint;
    private bool _isSelecting;
    private readonly Func<Rect, Task> _onAreaSelected;

    private SnippingOverlay(Func<Rect, Task> onAreaSelected)
    {
        _onAreaSelected = onAreaSelected;
        InitializeComponent();

        Loaded += (_, _) =>
        {
            ScreenCoordinates.CoverVirtualDesktop(this);
            // Căn giữa huy hiệu hướng dẫn
            OverlayCanvas.UpdateLayout();
            var badgeWidth = InstructionBadge.ActualWidth > 0 ? InstructionBadge.ActualWidth : 480;
            Canvas.SetLeft(InstructionBadge, (OverlayCanvas.ActualWidth - badgeWidth) / 2);
            Focus();
        };
    }

    private static SnippingOverlay? _currentOverlay;

    public static void StartSnipping(Func<Rect, Task> onAreaSelected)
    {
        ArgumentNullException.ThrowIfNull(onAreaSelected);
        // Nếu màn hình quét đang mở sẵn, bấm Alt+Q lần nữa sẽ hủy/đóng thay vì mở đè lên làm tối màn hình
        if (_currentOverlay != null)
        {
            _currentOverlay.Close();
            return;
        }

        var overlay = new SnippingOverlay(onAreaSelected);
        _currentOverlay = overlay;
        overlay.Closed += (_, _) => _currentOverlay = null;
        overlay.Show();
        overlay.Activate();
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Right)
        {
            Close();
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            _startPoint = e.GetPosition(OverlayCanvas);
            _startScreenPoint = OverlayCanvas.PointToScreen(_startPoint);
            _isSelecting = true;

            Canvas.SetLeft(SelectionBox, _startPoint.X);
            Canvas.SetTop(SelectionBox, _startPoint.Y);
            SelectionBox.Width = 0;
            SelectionBox.Height = 0;
            SelectionBox.Visibility = Visibility.Visible;

            CaptureMouse();
        }
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isSelecting) return;

        var currentPoint = e.GetPosition(OverlayCanvas);

        var x = Math.Min(_startPoint.X, currentPoint.X);
        var y = Math.Min(_startPoint.Y, currentPoint.Y);
        var width = Math.Abs(currentPoint.X - _startPoint.X);
        var height = Math.Abs(currentPoint.Y - _startPoint.Y);

        Canvas.SetLeft(SelectionBox, x);
        Canvas.SetTop(SelectionBox, y);
        SelectionBox.Width = width;
        SelectionBox.Height = height;
    }

    private async void Window_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting || e.ChangedButton != MouseButton.Left) return;

        _isSelecting = false;
        ReleaseMouseCapture();

        var currentPoint = e.GetPosition(OverlayCanvas);

        var pixelArea = new Rect(_startScreenPoint, OverlayCanvas.PointToScreen(currentPoint));
        Close();

        // Chỉ xử lý nếu vùng quét đủ lớn (lớn hơn 10x10 pixel để tránh click nhầm)
        if (pixelArea.Width > 10 && pixelArea.Height > 10)
        {
            // Let WPF remove the overlay before copying desktop pixels.
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            await _onAreaSelected(pixelArea);
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
