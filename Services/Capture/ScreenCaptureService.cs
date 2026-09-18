using System.Drawing;
using System.Windows;

namespace ScreenTranslator.Services.Capture;

public static class ScreenCaptureService
{
    private static readonly WindowsScreenCaptureService Service = new();

    public static Bitmap CaptureRegion(Rect area, double dpiScaleX = 1.0, double dpiScaleY = 1.0)
    {
        return Service.CaptureRegion(area, dpiScaleX, dpiScaleY);
    }
}
