using System.Collections.Concurrent;
using System.Drawing;
using Windows.Globalization;
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
        using var softwareBitmap = await Task.Run(() => OcrImageProcessor.CreateSoftwareBitmap(bitmap, cancellationToken), cancellationToken);

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

}
