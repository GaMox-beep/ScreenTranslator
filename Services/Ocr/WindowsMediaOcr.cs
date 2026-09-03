using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace ScreenTranslator.Services.Ocr;

public class WindowsMediaOcr : IOcrEngine
{
    public async Task<string> RecognizeTextAsync(Bitmap bitmap, string languageCode = "Auto Detect")
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        // 1. Chuyển đổi Bitmap sang SoftwareBitmap trong RAM qua MemoryStream
        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, ImageFormat.Bmp);
        memoryStream.Position = 0;

        using var randomAccessStream = memoryStream.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
        using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, 
            BitmapAlphaMode.Premultiplied);

        // 2. Khởi tạo OcrEngine tương ứng với ngôn ngữ yêu cầu hoặc ngôn ngữ máy
        var engine = CreateEngine(languageCode);
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
}
