using System.Drawing;
using System.Windows;

namespace ScreenTranslator.Services.Capture;

public static class ScreenCaptureService
{
    private static readonly WindowsScreenCaptureService Service = new();

    public static Bitmap CaptureRegion(Rect pixelArea)
    {
        return Service.CaptureRegion(pixelArea);
    }
}
