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
            cancellationToken.ThrowIfCancellationRequested();
            callbacks.OnStatusChanged("Đang chụp vùng chọn...", false);

            // 1. Chụp ảnh vùng chọn hoàn toàn trong RAM
            var captureSw = Stopwatch.StartNew();
            var ocrSw = new Stopwatch();
            string recognizedText;
            using (var bitmap = await Task.Run(() => _captureService.CaptureRegion(request.PixelArea), cancellationToken))
            {
                captureSw.Stop();
                cancellationToken.ThrowIfCancellationRequested();
                AppLogger.Log($"[CAPTURE] Completed in {captureSw.ElapsedMilliseconds} ms (Width={bitmap.Width}px, Height={bitmap.Height}px).");
                // The popup is shown only after pixels have been captured, so it cannot obscure the selection.
                callbacks.OnLoading();
                callbacks.OnProgress?.Invoke("Đang nhận diện chữ...");
                callbacks.OnStatusChanged("Đang nhận diện chữ...", false);
                ocrSw.Start();
                recognizedText = await _ocrEngine.RecognizeTextAsync(bitmap, request.SourceLanguage, cancellationToken);
                ocrSw.Stop();
            }

            cancellationToken.ThrowIfCancellationRequested();

            // 3. Nếu không có chữ hoặc chỉ là ký tự rác/dấu câu: dừng lại ngay (tiết kiệm 100% token)
            if (string.IsNullOrWhiteSpace(recognizedText) || !recognizedText.Any(char.IsLetterOrDigit))
            {
                AppLogger.Log($"[OCR] OCR completed in {ocrSw.ElapsedMilliseconds} ms: No recognizable words in selection (Length={recognizedText?.Length ?? 0}). Skipped translation.");
                callbacks.OnStatusChanged("Không tìm thấy văn bản nào trong vùng chọn.", false);
                callbacks.OnNoText?.Invoke();
                return;
            }

            AppLogger.Log($"[OCR] Completed in {ocrSw.ElapsedMilliseconds} ms (Length={recognizedText.Length} chars).");
            callbacks.OnOriginalText?.Invoke(recognizedText);

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
            callbacks.OnProgress?.Invoke("Đang dịch...");
            callbacks.OnStatusChanged("Đang dịch...", false);
            AppLogger.Log($"[TRANSLATE] Starting translation pipeline (Source={request.SourceLanguage}, Target={request.TargetLanguage}, InputChars={recognizedText.Length})...");

            var translatedText = await _translator.TranslateAsync(
                recognizedText,
                request.TargetLanguage,
                request.SourceLanguage,
                request.ApiKey,
                cancellationToken);

            translateSw.Stop();
            cancellationToken.ThrowIfCancellationRequested();
            callbacks.OnSuccess(translatedText);
            totalSw.Stop();

            AppLogger.Log($"[TRANSLATE] Finished in {translateSw.ElapsedMilliseconds} ms (OutputChars={translatedText.Length}).");
            AppLogger.Log($"[TOTAL] End-to-end pipeline finished in {totalSw.ElapsedMilliseconds} ms (Capture={captureSw.ElapsedMilliseconds}ms, OCR={ocrSw.ElapsedMilliseconds}ms, Translate={translateSw.ElapsedMilliseconds}ms).\n");

            callbacks.OnStatusChanged($"Đã dịch xong ({totalSw.ElapsedMilliseconds} ms).", false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AppLogger.Log("[CANCEL] Translation cancelled due to subsequent capture request.");
            callbacks.OnCancelled?.Invoke();
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                callbacks.OnCancelled?.Invoke();
                return;
            }
            totalSw.Stop();
            AppLogger.Log($"[ERROR] Pipeline execution failed after {totalSw.ElapsedMilliseconds} ms: Exception={ex.GetType().Name}, Message=\"{ex.Message}\"");
            callbacks.OnError(ex.Message);
            callbacks.OnStatusChanged($"Lỗi: {ex.Message}", true);
        }
    }
}
