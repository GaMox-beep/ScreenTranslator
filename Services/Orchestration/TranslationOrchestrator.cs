using System.Diagnostics;
using ScreenTranslator.Services.Capture;
using ScreenTranslator.Services.Ocr;
using ScreenTranslator.Services.Translation;

namespace ScreenTranslator.Services.Orchestration;

public class TranslationOrchestrator : ITranslationOrchestrator
{
    private readonly IScreenCaptureService _captureService;
    private readonly IOcrEngine _ocrEngine;
    private readonly ITranslator _translator;

    public TranslationOrchestrator(
        IScreenCaptureService captureService,
        IOcrEngine ocrEngine,
        ITranslator translator)
    {
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        _ocrEngine = ocrEngine ?? throw new ArgumentNullException(nameof(ocrEngine));
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
    }

    public async Task ProcessAsync(
        TranslationPipelineRequest request,
        TranslationPipelineCallbacks callbacks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(callbacks);

        var totalSw = Stopwatch.StartNew();

        try
        {
            callbacks.OnStatusChanged("Đang nhận diện chữ...", false);

            // 1. Chụp ảnh vùng chọn hoàn toàn trong RAM
            var captureSw = Stopwatch.StartNew();
            using var bitmap = _captureService.CaptureRegion(request.SelectedArea, request.DpiScaleX, request.DpiScaleY);
            captureSw.Stop();
            AppLogger.Log($"[CAPTURE] Screen capture completed in {captureSw.ElapsedMilliseconds} ms (Width={bitmap.Width}px, Height={bitmap.Height}px, DpiScale={request.DpiScaleX:0.00}x{request.DpiScaleY:0.00}).");

            cancellationToken.ThrowIfCancellationRequested();

            // 2. Nhận diện chữ bằng OCR Engine
            var ocrSw = Stopwatch.StartNew();
            var recognizedText = await _ocrEngine.RecognizeTextAsync(bitmap, request.SourceLanguage);
            ocrSw.Stop();

            cancellationToken.ThrowIfCancellationRequested();

            // 3. Nếu không có chữ hoặc chỉ là ký tự rác/dấu câu: dừng lại ngay (tiết kiệm 100% token)
            if (string.IsNullOrWhiteSpace(recognizedText) || !recognizedText.Any(char.IsLetterOrDigit))
            {
                AppLogger.Log($"[OCR] OCR completed in {ocrSw.ElapsedMilliseconds} ms: No recognizable words in selection (Length={recognizedText?.Length ?? 0}). Skipped translation.");
                callbacks.OnStatusChanged("Không tìm thấy văn bản nào trong vùng chọn.", false);
                return;
            }

            var cleanPreview = recognizedText.Replace(Environment.NewLine, " ");
            AppLogger.Log($"[OCR] OCR completed in {ocrSw.ElapsedMilliseconds} ms (Length={recognizedText.Length} chars): \"{cleanPreview}\"");

            // 4. Mở popup hiển thị trạng thái đang dịch
            callbacks.OnLoading();

            // 5. Kiểm tra API Key
            if (string.IsNullOrWhiteSpace(request.ApiKey))
            {
                const string apiKeyError = "Chưa có API Key. Vui lòng mở cửa sổ Cài đặt để nhập API Key Google Gemini.";
                callbacks.OnError(apiKeyError);
                callbacks.OnStatusChanged("Lỗi: Chưa cấu hình API Key.", true);
                AppLogger.Log("[TRANSLATE] ERROR: ApiKey is empty or not configured.");
                return;
            }

            // 6. Gọi dịch thuật
            var translateSw = Stopwatch.StartNew();
            AppLogger.Log($"[TRANSLATE] Starting translation pipeline (Source={request.SourceLanguage}, Target={request.TargetLanguage}, InputChars={recognizedText.Length})...");

            var translatedText = await _translator.TranslateAsync(
                recognizedText,
                request.TargetLanguage,
                request.SourceLanguage,
                request.ApiKey,
                cancellationToken);

            translateSw.Stop();
            callbacks.OnSuccess(translatedText);
            totalSw.Stop();

            var shortTranslated = translatedText.Replace(Environment.NewLine, " ");
            if (shortTranslated.Length > 100) shortTranslated = shortTranslated[..97] + "...";

            AppLogger.Log($"[TRANSLATE] Translation finished in {translateSw.ElapsedMilliseconds} ms (OutputChars={translatedText.Length}): \"{shortTranslated}\"");
            AppLogger.Log($"[TOTAL] End-to-end pipeline finished in {totalSw.ElapsedMilliseconds} ms (Capture={captureSw.ElapsedMilliseconds}ms, OCR={ocrSw.ElapsedMilliseconds}ms, Translate={translateSw.ElapsedMilliseconds}ms).\n");

            var previewText = cleanPreview.Length > 50 ? cleanPreview[..47] + "..." : cleanPreview;
            callbacks.OnStatusChanged($"Đã dịch ({translateSw.ElapsedMilliseconds}ms): \"{previewText}\"", false);
        }
        catch (OperationCanceledException)
        {
            AppLogger.Log("[CANCEL] Translation cancelled due to subsequent capture request.");
        }
        catch (Exception ex)
        {
            totalSw.Stop();
            AppLogger.Log($"[ERROR] Pipeline execution failed after {totalSw.ElapsedMilliseconds} ms: Exception={ex.GetType().Name}, Message=\"{ex.Message}\"");
            callbacks.OnError(ex.Message);
            callbacks.OnStatusChanged($"Lỗi: {ex.Message}", true);
        }
    }
}
