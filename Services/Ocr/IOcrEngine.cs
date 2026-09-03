using System.Drawing;

namespace ScreenTranslator.Services.Ocr;

public interface IOcrEngine
{
    Task<string> RecognizeTextAsync(Bitmap bitmap, string languageCode = "Auto Detect");
}
