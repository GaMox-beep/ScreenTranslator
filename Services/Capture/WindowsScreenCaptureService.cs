using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;

namespace ScreenTranslator.Services.Capture;

public class WindowsScreenCaptureService : IScreenCaptureService
{
    public Bitmap CaptureRegion(Rect pixelArea)
    {
        var physicalX = checked((int)Math.Round(pixelArea.Left));
        var physicalY = checked((int)Math.Round(pixelArea.Top));
        var physicalWidth = checked((int)Math.Round(pixelArea.Right) - physicalX);
        var physicalHeight = checked((int)Math.Round(pixelArea.Bottom) - physicalY);

        if (physicalWidth <= 0 || physicalHeight <= 0)
        {
            throw new ArgumentException("Kích thước vùng chụp phải lớn hơn 0.");
        }

        var bitmap = new Bitmap(physicalWidth, physicalHeight, PixelFormat.Format32bppArgb);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(
                physicalX, physicalY,
                0, 0,
                new System.Drawing.Size(physicalWidth, physicalHeight),
                CopyPixelOperation.SourceCopy);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}
