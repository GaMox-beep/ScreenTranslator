using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace ScreenTranslator.Services.Ocr;

public class WindowsMediaOcr : IOcrEngine
{
    private static readonly ConcurrentDictionary<string, OcrEngine> EngineCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> RecognizeTextAsync(Bitmap bitmap, string languageCode = "Windows Default", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        cancellationToken.ThrowIfCancellationRequested();
        var engine = EngineCache.GetOrAdd(languageCode, CreateEngine);

        // Preprocess on a worker so large selections do not block the WPF dispatcher.
        using var preprocessed = await Task.Run(() => PreprocessBitmap(bitmap, cancellationToken), cancellationToken);

        // 2. Chuyển đổi Bitmap sang SoftwareBitmap trong RAM qua MemoryStream
        using var memoryStream = new MemoryStream();
        preprocessed.Save(memoryStream, ImageFormat.Bmp);
        memoryStream.Position = 0;

        using var randomAccessStream = memoryStream.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(randomAccessStream).AsTask(cancellationToken);
        using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied).AsTask(cancellationToken);

        // 3. Thực hiện nhận diện chữ
        var ocrResult = await engine.RecognizeAsync(softwareBitmap).AsTask(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (ocrResult == null || ocrResult.Lines.Count == 0)
        {
            return string.Empty;
        }

        // 4. Ghép các dòng chữ lại thành đoạn văn bản hoàn chỉnh
        var lines = ocrResult.Lines.Select(line => line.Text);
        return string.Join(Environment.NewLine, lines).Trim();
    }

    private static OcrEngine CreateEngine(string language)
    {
        var tag = MapLanguageToTag(language);
        if (tag != null)
        {
            var targetLang = new Language(tag);
            if (OcrEngine.IsLanguageSupported(targetLang))
            {
                var targetEngine = OcrEngine.TryCreateFromLanguage(targetLang);
                if (targetEngine != null) return targetEngine;
            }
            throw new InvalidOperationException($"Windows chưa cài OCR cho {language}. Vui lòng cài gói ngôn ngữ tương ứng trong Windows Settings.");
        }

        // Windows Default uses the first supported language in the Windows user profile.
        var userEngine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (userEngine != null) return userEngine;

        // Dự phòng 2: Dùng bất kỳ ngôn ngữ OCR nào đang có sẵn trên máy
        var available = OcrEngine.AvailableRecognizerLanguages;
        if (available != null && available.Count > 0)
        {
            var fallbackEngine = OcrEngine.TryCreateFromLanguage(available[0]);
            if (fallbackEngine != null) return fallbackEngine;
        }

        throw new InvalidOperationException("Hệ điều hành Windows hiện chưa cài gói ngôn ngữ OCR phù hợp.");
    }

    private static string? MapLanguageToTag(string language) => language switch
    {
        "English" => "en-US",
        "Japanese" => "ja-JP",
        "Chinese" => "zh-Hans-CN",
        "Korean" => "ko-KR",
        "French" => "fr-FR",
        "German" => "de-DE",
        "Spanish" => "es-ES",
        "Russian" => "ru-RU",
        "Windows Default" or "Auto Detect" => null,
        _ => throw new ArgumentException($"Ngôn ngữ OCR không được hỗ trợ: {language}", nameof(language))
    };

    private static Bitmap PreprocessBitmap(Bitmap source, CancellationToken cancellationToken)
    {
        // Upscale short selections to improve recognition of small text.
        double scale = 1.0;
        if (source.Height < 140)
        {
            scale = 2.0;
        }
        else if (source.Height < 260)
        {
            scale = 1.5;
        }

        // WinRT limits both dimensions, including after upscaling small text.
        var maxDimension = (int)OcrEngine.MaxImageDimension;
        scale = Math.Min(scale, (double)maxDimension / Math.Max(source.Width, source.Height));
        int targetWidth = Math.Clamp((int)Math.Round(source.Width * scale), 1, maxDimension);
        int targetHeight = Math.Clamp((int)Math.Round(source.Height * scale), 1, maxDimension);

        var scaled = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);
        try
        {
            using (var g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source, 0, 0, targetWidth, targetHeight);
            }

            // Boost contrast in place, respecting the bitmap's row stride.
            var rect = new Rectangle(0, 0, targetWidth, targetHeight);
            var bmpData = scaled.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

            try
            {
                unsafe
                {
                    for (int y = 0; y < targetHeight; y++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        byte* ptr = (byte*)bmpData.Scan0 + y * bmpData.Stride;
                        for (int x = 0; x < targetWidth; x++)
                        {
                            byte b = ptr[0];
                            byte g = ptr[1];
                            byte r = ptr[2];

                            // Luminance: 0.299R + 0.587G + 0.114B
                            int gray = (r * 77 + g * 150 + b * 29) >> 8;

                            byte enhanced = (byte)(gray > 140 ? Math.Min(255, (gray * 13) / 10) : (gray * 7) / 10);

                            ptr[0] = enhanced;
                            ptr[1] = enhanced;
                            ptr[2] = enhanced;

                            ptr += 4;
                        }
                    }
                }
            }
            finally
            {
                scaled.UnlockBits(bmpData);
            }
            return scaled;
        }
        catch
        {
            scaled.Dispose();
            throw;
        }
    }
}

