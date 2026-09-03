using System.Drawing;
using System.Windows;

namespace ScreenTranslator.Services.Capture;

public static class ScreenCaptureService
{
    public static Bitmap CaptureRegion(Rect area, double dpiScaleX = 1.0, double dpiScaleY = 1.0)
    {
        var physicalX = (int)Math.Round(area.X * dpiScaleX);
        var physicalY = (int)Math.Round(area.Y * dpiScaleY);
        var physicalWidth = (int)Math.Round(area.Width * dpiScaleX);
        var physicalHeight = (int)Math.Round(area.Height * dpiScaleY);

        if (physicalWidth <= 0 || physicalHeight <= 0)
        {
            throw new ArgumentException("Kích thước vùng chụp phải lớn hơn 0.");
        }

        var bitmap = new Bitmap(physicalWidth, physicalHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(
                physicalX, physicalY, 
                0, 0, 
                new System.Drawing.Size(physicalWidth, physicalHeight), 
                CopyPixelOperation.SourceCopy);
        }

        return bitmap;
    }
}
