using System.Drawing;
using System.Windows;

namespace ScreenTranslator.Services.Capture;

public interface IScreenCaptureService
{
    Bitmap CaptureRegion(Rect area, double dpiScaleX = 1.0, double dpiScaleY = 1.0);
}
