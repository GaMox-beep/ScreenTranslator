using System.Drawing;
using System.Windows;

namespace ScreenTranslator.Services.Capture;

public interface IScreenCaptureService
{
    /// <summary>Captures an absolute rectangle in physical desktop pixels.</summary>
    Bitmap CaptureRegion(Rect pixelArea);
}
