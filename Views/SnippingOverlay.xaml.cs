using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ScreenTranslator.Views;

public partial class SnippingOverlay : Window
{
    private Point _startPoint;
    private bool _isSelecting;

    public event Action<Rect>? AreaSelected;

    public SnippingOverlay()
    {
        InitializeComponent();

        // Bao phủ toàn bộ các màn hình
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        Loaded += (_, _) =>
        {
            // Căn giữa huy hiệu hướng dẫn
            var badgeWidth = InstructionBadge.ActualWidth > 0 ? InstructionBadge.ActualWidth : 480;
            Canvas.SetLeft(InstructionBadge, (Width - badgeWidth) / 2);
            Focus();
        };
    }

    private static SnippingOverlay? _currentOverlay;

    public static void StartSnipping(Action<Rect> onAreaSelected)
    {
        // Nếu màn hình quét đang mở sẵn, bấm Alt+Q lần nữa sẽ hủy/đóng thay vì mở đè lên làm tối màn hình
        if (_currentOverlay != null)
        {
            _currentOverlay.Close();
            return;
        }

        var overlay = new SnippingOverlay();
        _currentOverlay = overlay;
        overlay.Closed += (_, _) => _currentOverlay = null;
        overlay.AreaSelected += onAreaSelected;
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

    private void Window_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting || e.ChangedButton != MouseButton.Left) return;

        _isSelecting = false;
        ReleaseMouseCapture();

        var currentPoint = e.GetPosition(OverlayCanvas);

        var x = Math.Min(_startPoint.X, currentPoint.X);
        var y = Math.Min(_startPoint.Y, currentPoint.Y);
        var width = Math.Abs(currentPoint.X - _startPoint.X);
        var height = Math.Abs(currentPoint.Y - _startPoint.Y);

        Close();

        // Chỉ xử lý nếu vùng quét đủ lớn (lớn hơn 10x10 pixel để tránh click nhầm)
        if (width > 10 && height > 10)
        {
            var absoluteRect = new Rect(x + Left, y + Top, width, height);
            AreaSelected?.Invoke(absoluteRect);
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
