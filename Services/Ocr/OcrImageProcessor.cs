using System.Buffers;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;

namespace ScreenTranslator.Services.Ocr;

internal static class OcrImageProcessor
{
    internal static Bitmap PrepareBitmap(Bitmap source, int maxDimension, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDimension);
        cancellationToken.ThrowIfCancellationRequested();
        var scale = source.Height < 140 ? 2.0 : source.Height < 260 ? 1.5 : 1.0;
        scale = Math.Min(scale, (double)maxDimension / Math.Max(source.Width, source.Height));
        var width = Math.Clamp((int)Math.Round(source.Width * scale), 1, maxDimension);
        var height = Math.Clamp((int)Math.Round(source.Height * scale), 1, maxDimension);
        var scaled = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        try
        {
            using (var graphics = Graphics.FromImage(scaled))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, 0, 0, width, height);
            }
            var data = scaled.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try { NormalizeContrast(data, width, height, cancellationToken); }
            finally { scaled.UnlockBits(data); }
            return scaled;
        }
        catch
        {
            scaled.Dispose();
            throw;
        }
    }

    private static unsafe void NormalizeContrast(BitmapData data, int width, int height, CancellationToken token)
    {
        Span<int> histogram = stackalloc int[256];
        histogram.Clear();
        long luminanceSum = 0;
        for (int y = 0; y < height; y++)
        {
            token.ThrowIfCancellationRequested();
            byte* pixel = (byte*)data.Scan0 + y * data.Stride;
            for (int x = 0; x < width; x++, pixel += 4)
            {
                byte gray = (byte)((pixel[2] * 77 + pixel[1] * 150 + pixel[0] * 29) >> 8);
                histogram[gray]++;
                luminanceSum += gray;
                pixel[0] = pixel[1] = pixel[2] = gray;
                pixel[3] = 255;
            }
        }
        var count = (long)width * height;
        var low = Percentile(histogram, Math.Max(1, count / 50));
        var high = Percentile(histogram, Math.Max(1, count * 49 / 50));
        var range = high - low;
        var gain = range >= 32 ? Math.Min(2.0, 255.0 / range) : 1.0;
        var center = range >= 32 ? (low + high) / 2.0 : 127.5;
        var invert = luminanceSum < count * 128;
        Span<byte> lookup = stackalloc byte[256];
        for (int gray = 0; gray < lookup.Length; gray++)
        {
            var enhanced = (byte)Math.Clamp((int)Math.Round((gray - center) * gain + 127.5), 0, 255);
            lookup[gray] = invert ? (byte)(255 - enhanced) : enhanced;
        }
        for (int y = 0; y < height; y++)
        {
            token.ThrowIfCancellationRequested();
            byte* pixel = (byte*)data.Scan0 + y * data.Stride;
            for (int x = 0; x < width; x++, pixel += 4)
                pixel[0] = pixel[1] = pixel[2] = lookup[pixel[0]];
        }
    }

    private static int Percentile(ReadOnlySpan<int> histogram, long rank)
    {
        long count = 0;
        for (int gray = 0; gray < histogram.Length; gray++)
        {
            count += histogram[gray];
            if (count >= rank) return gray;
        }
        return 255;
    }

    internal static SoftwareBitmap CreateSoftwareBitmap(Bitmap source, CancellationToken token = default)
    {
        using var prepared = PrepareBitmap(source, (int)Windows.Media.Ocr.OcrEngine.MaxImageDimension, token);
        var rowBytes = checked(prepared.Width * 4);
        var length = checked(rowBytes * prepared.Height);
        var pixels = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            var data = prepared.LockBits(new Rectangle(0, 0, prepared.Width, prepared.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < prepared.Height; y++)
                {
                    token.ThrowIfCancellationRequested();
                    Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * rowBytes, rowBytes);
                }
            }
            finally { prepared.UnlockBits(data); }
            token.ThrowIfCancellationRequested();
            return SoftwareBitmap.CreateCopyFromBuffer(pixels.AsBuffer(0, length), BitmapPixelFormat.Bgra8,
                prepared.Width, prepared.Height, BitmapAlphaMode.Ignore);
        }
        finally { ArrayPool<byte>.Shared.Return(pixels, clearArray: true); }
    }
}
