using System.Windows;
using ScreenTranslator.Services.Orchestration;
using ScreenTranslator.Tests.Fakes;

namespace ScreenTranslator.Tests;

public class OrchestratorTests
{
    private readonly FakeScreenCaptureService _captureService = new();
    private readonly FakeOcrEngine _ocrEngine = new();
    private readonly FakeTranslator _translator = new();
    private readonly TranslationOrchestrator _sut; // System Under Test

    public OrchestratorTests()
    {
        _sut = new TranslationOrchestrator(_captureService, _ocrEngine, _translator);
    }

    [Fact]
    public async Task ProcessAsync_WhenOcrReturnsValidText_ExecutesFullPipelineSuccessfully()
    {
        // Arrange
        var request = new TranslationPipelineRequest(
            new Rect(10, 20, 300, 200),
            1.25, 1.25,
            "English", "Vietnamese",
            "fake-api-key");

        bool loadingCalled = false;
        string? resultText = null;
        string? errorText = null;
        string? statusText = null;

        var callbacks = new TranslationPipelineCallbacks(
            OnLoading: () => loadingCalled = true,
            OnSuccess: text => resultText = text,
            OnError: err => errorText = err,
            OnStatusChanged: (status, _) => statusText = status);

        _ocrEngine.TextToReturn = "Sign In to continue";
        _translator.TranslationToReturn = "Đăng nhập để tiếp tục";

        // Act
        await _sut.ProcessAsync(request, callbacks);

        // Assert
        Assert.Equal(1, _captureService.CaptureCallCount);
        Assert.Equal(1, _ocrEngine.RecognizeCallCount);
        Assert.Equal(1, _translator.TranslateCallCount);
        Assert.True(loadingCalled);
        Assert.Equal("Đăng nhập để tiếp tục", resultText);
        Assert.Null(errorText);
        Assert.NotNull(statusText);
        Assert.Contains("Đã dịch", statusText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData("!@#$%^&*()")]
    [InlineData("\r\n \t")]
    public async Task ProcessAsync_WhenOcrReturnsWhitespaceOrPunctuationOnly_SkipsTranslation(string ocrGarbage)
    {
        // Arrange
        var request = new TranslationPipelineRequest(
            new Rect(0, 0, 100, 100),
            1.0, 1.0,
            "English", "Vietnamese",
            "fake-api-key");

        bool loadingCalled = false;
        string? resultText = null;
        string? statusText = null;

        var callbacks = new TranslationPipelineCallbacks(
            OnLoading: () => loadingCalled = true,
            OnSuccess: text => resultText = text,
            OnError: _ => { },
            OnStatusChanged: (status, _) => statusText = status);

        _ocrEngine.TextToReturn = ocrGarbage;

        // Act
        await _sut.ProcessAsync(request, callbacks);

        // Assert: Không được gọi translator và không mở loading popup
        Assert.Equal(0, _translator.TranslateCallCount);
        Assert.False(loadingCalled);
        Assert.Null(resultText);
        Assert.Equal("Không tìm thấy văn bản nào trong vùng chọn.", statusText);
    }

    [Fact]
    public async Task ProcessAsync_WhenApiKeyIsEmpty_CallsOnErrorWithoutCallingTranslator()
    {
        // Arrange
        var request = new TranslationPipelineRequest(
            new Rect(0, 0, 100, 100),
            1.0, 1.0,
            "English", "Vietnamese",
            ""); // Empty API Key

        string? errorText = null;
        var callbacks = new TranslationPipelineCallbacks(
            OnLoading: () => { },
            OnSuccess: _ => { },
            OnError: err => errorText = err,
            OnStatusChanged: (_, _) => { });

        _ocrEngine.TextToReturn = "Valid text";

        // Act
        await _sut.ProcessAsync(request, callbacks);

        // Assert
        Assert.Equal(0, _translator.TranslateCallCount);
        Assert.NotNull(errorText);
        Assert.Contains("API Key", errorText);
    }

    [Fact]
    public async Task ProcessAsync_WhenCancelled_HandlesCancellationWithoutThrowing()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var request = new TranslationPipelineRequest(
            new Rect(0, 0, 100, 100),
            1.0, 1.0,
            "English", "Vietnamese",
            "fake-key");

        _ocrEngine.TextToReturn = "Long text being processed";
        _translator.Delay = TimeSpan.FromSeconds(5); // Giả lập mạng chậm

        var callbacks = new TranslationPipelineCallbacks(
            OnLoading: () => { },
            OnSuccess: _ => { },
            OnError: _ => { },
            OnStatusChanged: (_, _) => { });

        // Act
        cts.Cancel(); // Hủy ngay trước/trong khi chạy
        var exception = await Record.ExceptionAsync(() => _sut.ProcessAsync(request, callbacks, cts.Token));

        // Assert: Không ném uncaught exception làm sập ứng dụng
        Assert.Null(exception);
    }

    [Fact]
    public async Task ProcessAsync_WhenTranslatorThrows_PropagatesErrorToCallbacks()
    {
        // Arrange
        var request = new TranslationPipelineRequest(
            new Rect(0, 0, 100, 100),
            1.0, 1.0,
            "English", "Vietnamese",
            "fake-key");

        string? receivedError = null;
        bool isStatusError = false;

        var callbacks = new TranslationPipelineCallbacks(
            OnLoading: () => { },
            OnSuccess: _ => { },
            OnError: err => receivedError = err,
            OnStatusChanged: (_, isErr) => isStatusError = isErr);

        _ocrEngine.TextToReturn = "Valid text";
        _translator.ExceptionToThrow = new InvalidOperationException("API Quota Exceeded (429)");

        // Act
        await _sut.ProcessAsync(request, callbacks);

        // Assert
        Assert.NotNull(receivedError);
        Assert.Contains("429", receivedError);
        Assert.True(isStatusError);
    }
}
