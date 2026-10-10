using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ScreenTranslator.Services.Ocr;
using Xunit.Abstractions;

namespace ScreenTranslator.Tests;

public sealed class OcrImageProcessorTests(ITestOutputHelper output)
{
    [Fact]
    public void PixelBufferConversion_AvoidsBmpStreamAllocation()
    {
        using var original = new Bitmap(1920, 1080);
        using (OcrImageProcessor.CreateSoftwareBitmap(original)) { }
        var directStart = GC.GetAllocatedBytesForCurrentThread();
        using (OcrImageProcessor.CreateSoftwareBitmap(original)) { }
        var directAllocated = GC.GetAllocatedBytesForCurrentThread() - directStart;
        var encodedStart = GC.GetAllocatedBytesForCurrentThread();
        using (var prepared = OcrImageProcessor.PrepareBitmap(original, 4096))
        using (var stream = new MemoryStream()) prepared.Save(stream, ImageFormat.Bmp);
        var encodedAllocated = GC.GetAllocatedBytesForCurrentThread() - encodedStart;
        output.WriteLine($"1920x1080 managed allocations: pixel buffer={directAllocated:N0} bytes; BMP stream={encodedAllocated:N0} bytes. Native bitmap allocations are excluded.");
        Assert.True(directAllocated < encodedAllocated, "Direct conversion should avoid the managed BMP stream allocation after pool warmup.");
    }

    [Fact]
    public void GrayscaleGradient_DoesNotIntroduceThresholdJumpAt140()
    {
        using var original = new Bitmap(256, 300);
        for (int x = 0; x < original.Width; x++)
            for (int y = 0; y < original.Height; y++)
                original.SetPixel(x, y, Color.FromArgb(x, x, x));
        using var processed = OcrImageProcessor.PrepareBitmap(original, 4096);
        var before = processed.GetPixel(140, 150).R;
        var after = processed.GetPixel(141, 150).R;
        Assert.InRange(Math.Abs(after - before), 0, 3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LightAndDarkBackgrounds_KeepTextVisibleAsDarkOnLight(bool dark)
    {
        using var original = new Bitmap(300, 300);
        using (var graphics = Graphics.FromImage(original))
        {
            graphics.Clear(dark ? Color.FromArgb(30, 30, 30) : Color.FromArgb(225, 225, 225));
            using var brush = new SolidBrush(dark ? Color.FromArgb(220, 220, 220) : Color.FromArgb(35, 35, 35));
            graphics.FillRectangle(brush, 100, 100, 80, 80);
        }
        using var processed = OcrImageProcessor.PrepareBitmap(original, 4096);
        Assert.True(processed.GetPixel(20, 20).R - processed.GetPixel(130, 130).R >= 150);
    }

    [Theory]
    [InlineData(500, 1500)]
    [InlineData(1500, 100)]
    public void LargeOrUpscaledImages_RespectBothDimensionLimits(int width, int height)
    {
        using var original = new Bitmap(width, height);
        using var processed = OcrImageProcessor.PrepareBitmap(original, 1000);
        Assert.InRange(processed.Width, 1, 1000);
        Assert.InRange(processed.Height, 1, 1000);
        Assert.InRange(Math.Abs((double)processed.Width / processed.Height - (double)width / height), 0, 0.2);
    }

    [Fact]
    public void SoftwareBitmapConversion_UsesPixelBufferAndKeepsValidDimensions()
    {
        using var original = new Bitmap(300, 300);
        using var bitmap = OcrImageProcessor.CreateSoftwareBitmap(original);
        Assert.Equal(300, bitmap.PixelWidth);
        Assert.Equal(300, bitmap.PixelHeight);
        Assert.Equal(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, bitmap.BitmapPixelFormat);
    }

    [Fact]
    public void CancelledConversion_DoesNotStartProcessing()
    {
        using var original = new Bitmap(20, 20);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => OcrImageProcessor.CreateSoftwareBitmap(original, cts.Token));
    }
}
