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
    private static readonly ConcurrentDictionary<string, OcrEngine?> EngineCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> RecognizeTextAsync(Bitmap bitmap, string languageCode = "Auto Detect")
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        // 1. Tiền xử lý ảnh siêu tốc (chỉ phóng to khi chữ nhỏ + tăng tương phản lọc nhiễu nền, 1-3ms)
        using var preprocessed = PreprocessBitmap(bitmap);

        // 2. Chuyển đổi Bitmap sang SoftwareBitmap trong RAM qua MemoryStream
        using var memoryStream = new MemoryStream();
        preprocessed.Save(memoryStream, ImageFormat.Bmp);
        memoryStream.Position = 0;

        using var randomAccessStream = memoryStream.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
        using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, 
            BitmapAlphaMode.Premultiplied);

        // 2. Lấy OcrEngine từ cache (tránh tải lại từ điển hệ thống mỗi lần quét)
        var engine = EngineCache.GetOrAdd(languageCode, CreateEngine);
        if (engine == null)
        {
            throw new InvalidOperationException("Hệ điều hành Windows hiện chưa cài gói ngôn ngữ OCR phù hợp.");
        }

        // 3. Thực hiện nhận diện chữ
        var ocrResult = await engine.RecognizeAsync(softwareBitmap);
        if (ocrResult == null || ocrResult.Lines.Count == 0)
        {
            return string.Empty;
        }

        // 4. Ghép các dòng chữ lại thành đoạn văn bản hoàn chỉnh
        var lines = ocrResult.Lines.Select(line => line.Text);
        return string.Join(Environment.NewLine, lines).Trim();
    }

    private static OcrEngine? CreateEngine(string language)
    {
        var tag = MapLanguageToTag(language);
        if (!string.IsNullOrEmpty(tag))
        {
            var targetLang = new Language(tag);
            if (OcrEngine.IsLanguageSupported(targetLang))
            {
                var targetEngine = OcrEngine.TryCreateFromLanguage(targetLang);
                if (targetEngine != null) return targetEngine;
            }
        }

        // Dự phòng 1: Dùng ngôn ngữ mặc định của người dùng Windows
        var userEngine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (userEngine != null) return userEngine;

        // Dự phòng 2: Dùng bất kỳ ngôn ngữ OCR nào đang có sẵn trên máy
        var available = OcrEngine.AvailableRecognizerLanguages;
        if (available != null && available.Count > 0)
        {
            return OcrEngine.TryCreateFromLanguage(available[0]);
        }

        return null;
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
        _ => "en-US"
    };

    private static Bitmap PreprocessBitmap(Bitmap source)
    {
        // 1. Chỉ phóng to khi ảnh chụp có chiều cao nhỏ (chữ nhỏ < 260px) để tối ưu triệt để CPU/RAM
        double scale = 1.0;
        if (source.Height < 140)
        {
            scale = 2.0;
        }
        else if (source.Height < 260)
        {
            scale = 1.5;
        }

        int targetWidth = (int)Math.Round(source.Width * scale);
        int targetHeight = (int)Math.Round(source.Height * scale);

        // Giới hạn chiều rộng tối đa an toàn cho RAM và Windows OCR
        if (targetWidth > 6000)
        {
            targetWidth = 6000;
            targetHeight = Math.Max(1, (int)Math.Round((double)source.Height * 6000 / source.Width));
        }

        var scaled = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(source, 0, 0, targetWidth, targetHeight);
        }

        // 2. Tăng tương phản phi tuyến bằng LockBits siêu tốc (1-2 ms, 0 cấp phát bộ nhớ heap)
        var rect = new Rectangle(0, 0, targetWidth, targetHeight);
        var bmpData = scaled.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

        try
        {
            unsafe
            {
                byte* ptr = (byte*)bmpData.Scan0;
                int totalPixels = targetWidth * targetHeight;

                for (int i = 0; i < totalPixels; i++)
                {
                    byte b = ptr[0];
                    byte g = ptr[1];
                    byte r = ptr[2];

                    // Luminance: 0.299R + 0.587G + 0.114B
                    int gray = (r * 77 + g * 150 + b * 29) >> 8;

                    // Tách chữ sáng khỏi viền/nền chuyển màu và làm đậm nét chữ
                    byte enhanced = (byte)(gray > 140 ? Math.Min(255, (gray * 13) / 10) : (gray * 7) / 10);

                    ptr[0] = enhanced;
                    ptr[1] = enhanced;
                    ptr[2] = enhanced;

                    ptr += 4;
                }
            }
        }
        finally
        {
            scaled.UnlockBits(bmpData);
        }

        return scaled;
    }
}

