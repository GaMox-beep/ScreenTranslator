using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using ScreenTranslator.Models;
using ScreenTranslator.Services.Capture;
using ScreenTranslator.Services.Ocr;
using ScreenTranslator.Services.Storage;
using ScreenTranslator.Services.Translation;

namespace ScreenTranslator.Tests.Fakes;

public class FakeScreenCaptureService : IScreenCaptureService
{
    public int CaptureCallCount { get; private set; }
    public Rect LastCapturedArea { get; private set; }

    public Bitmap CaptureRegion(Rect area, double dpiScaleX = 1.0, double dpiScaleY = 1.0)
    {
        CaptureCallCount++;
        LastCapturedArea = area;
        return new Bitmap(100, 100, PixelFormat.Format32bppArgb);
    }
}

public class FakeOcrEngine : IOcrEngine
{
    public string TextToReturn { get; set; } = "Hello World";
    public int RecognizeCallCount { get; private set; }
    public string? LastLanguageCode { get; private set; }

    public Task<string> RecognizeTextAsync(Bitmap bitmap, string languageCode = "Auto Detect")
    {
        RecognizeCallCount++;
        LastLanguageCode = languageCode;
        return Task.FromResult(TextToReturn);
    }
}

public class FakeTranslator : ITranslator
{
    public string TranslationToReturn { get; set; } = "Xin chào Thế giới";
    public int TranslateCallCount { get; private set; }
    public string? LastInputText { get; private set; }
    public string? LastTargetLang { get; private set; }
    public string? LastApiKey { get; private set; }
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;
    public Exception? ExceptionToThrow { get; set; }

    public async Task<string> TranslateAsync(
        string text,
        string targetLanguage,
        string sourceLanguage,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        TranslateCallCount++;
        LastInputText = text;
        LastTargetLang = targetLanguage;
        LastApiKey = apiKey;

        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        return TranslationToReturn;
    }
}

public class InMemorySettingsService : ISettingsService
{
    private AppSettings _settings = new();

    public string? LastError { get; set; }

    public AppSettings Load() => _settings;

    public bool Save(AppSettings settings)
    {
        _settings = settings;
        return true;
    }

    public void SaveLastWorkingModel(string model)
    {
        _settings.LastWorkingModel = model;
    }
}
