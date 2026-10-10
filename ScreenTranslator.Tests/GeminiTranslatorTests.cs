using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ScreenTranslator.Models;
using ScreenTranslator.Services.Translation;
using ScreenTranslator.Tests.Fakes;

namespace ScreenTranslator.Tests;

public class GeminiTranslatorTests
{
    private sealed class AsyncHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => responder(request, cancellationToken);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TranslateAsync_WhenCancelledDuringGeneration_StopsWithoutRotating(bool useFastPath)
    {
        var storage = new InMemorySettingsService();
        if (useFastPath)
            storage.Save(new AppSettings { LastWorkingModel = "models/test-gemini-flash" });

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var generationCalls = 0;
        var handler = new AsyncHttpMessageHandler(async (request, token) =>
        {
            if (request.Method == HttpMethod.Get)
                return JsonResponse("""{"models":[{"name":"models/test-gemini-flash","supportedGenerationMethods":["generateContent"]}]}""");

            Interlocked.Increment(ref generationCalls);
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return JsonResponse("{}");
        });
        using var client = new HttpClient(handler);
        var translator = new GoogleGeminiTranslator(client, storage);
        using var cts = new CancellationTokenSource();
        var translation = translator.TranslateAsync("Hello", "Vietnamese", "English", "test-key", cts.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => translation);
        Assert.Equal(1, generationCalls);
    }

    [Fact]
    public async Task TranslateAsync_WhenApiKeyChanges_DiscoversModelsForNewKey()
    {
        var discoveredKeys = new List<string>();
        var handler = new AsyncHttpMessageHandler((request, _) =>
        {
            var key = Assert.Single(request.Headers.GetValues("x-goog-api-key"));
            Assert.DoesNotContain("key=", request.RequestUri!.Query);
            if (request.Method == HttpMethod.Get)
            {
                discoveredKeys.Add(key);
                return Task.FromResult(JsonResponse($$"""{"models":[{"name":"models/gemini-{{key}}-flash","supportedGenerationMethods":["generateContent"]}]}"""));
            }
            Assert.Contains($"gemini-{key}-flash", request.RequestUri.AbsolutePath);
            return Task.FromResult(JsonResponse("""{"candidates":[{"content":{"parts":[{"text":"Xin chào"}]}}]}"""));
        });
        using var client = new HttpClient(handler);
        var translator = new GoogleGeminiTranslator(client, new InMemorySettingsService());
        await translator.TranslateAsync("Hello", "Vietnamese", "English", "first");
        await translator.TranslateAsync("Hello", "Vietnamese", "English", "second");
        Assert.Equal(new[] { "first", "second" }, discoveredKeys);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    [Fact]
    public async Task TranslateAsync_WithConcurrentApiKeys_SerializesGenerationAndKeepsModelsIsolated()
    {
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeGenerations = 0;
        var maxActiveGenerations = 0;
        var handler = new AsyncHttpMessageHandler(async (request, token) =>
        {
            var key = Assert.Single(request.Headers.GetValues("x-goog-api-key"));
            if (request.Method == HttpMethod.Get)
                return JsonResponse($$"""{"models":[{"name":"models/gemini-{{key}}-flash","supportedGenerationMethods":["generateContent"]}]}""");

            Assert.Contains($"gemini-{key}-flash", request.RequestUri!.AbsolutePath);
            var active = Interlocked.Increment(ref activeGenerations);
            maxActiveGenerations = Math.Max(maxActiveGenerations, active);
            try
            {
                if (key == "first")
                {
                    firstStarted.TrySetResult();
                    await releaseFirst.Task.WaitAsync(token);
                }
                return JsonResponse("""{"candidates":[{"content":{"parts":[{"text":"Xin chào"}]}}]}""");
            }
            finally
            {
                Interlocked.Decrement(ref activeGenerations);
            }
        });
        using var client = new HttpClient(handler);
        var translator = new GoogleGeminiTranslator(client, new InMemorySettingsService());
        var first = translator.TranslateAsync("Hello", "Vietnamese", "English", "first");
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = translator.TranslateAsync("Hello", "Vietnamese", "English", "second");
        releaseFirst.TrySetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, maxActiveGenerations);
    }

    [Fact]
    public async Task TranslateAsync_WhenAllModelsAreOnCooldown_DoesNotSendAnotherGenerationRequest()
    {
        var generationCalls = 0;
        var handler = new AsyncHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Get)
                return Task.FromResult(JsonResponse("""{"models":[{"name":"models/test-gemini-flash","supportedGenerationMethods":["generateContent"]}]}"""));
            generationCalls++;
            var response = JsonResponse("""{"error":{"code":429,"message":"Quota exceeded"}}""");
            response.StatusCode = HttpStatusCode.TooManyRequests;
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);
        var translator = new GoogleGeminiTranslator(client, new InMemorySettingsService());

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => translator.TranslateAsync("Hello", "Vietnamese", "English", "test-key"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => translator.TranslateAsync("Hello again", "Vietnamese", "English", "test-key"));
        Assert.Equal(1, generationCalls);
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);
        public List<HttpRequestMessage> SentRequests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SentRequests.Add(request);
            return Task.FromResult(Responder(request));
        }
    }

    [Fact]
    public async Task TranslateAsync_WhenApiKeyIsEmpty_ThrowsInvalidOperationException()
    {
        var translator = new GoogleGeminiTranslator(new HttpClient(new MockHttpMessageHandler()), new InMemorySettingsService());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            translator.TranslateAsync("Hello", "Vietnamese", "English", "   "));
    }

    [Fact]
    public async Task TranslateAsync_WhenTextIsEmpty_ReturnsEmptyImmediately()
    {
        var handler = new MockHttpMessageHandler();
        var translator = new GoogleGeminiTranslator(new HttpClient(handler), new InMemorySettingsService());

        var result = await translator.TranslateAsync("", "Vietnamese", "English", "valid-key");

        Assert.Equal(string.Empty, result);
        Assert.Empty(handler.SentRequests);
    }

    [Fact]
    public async Task TranslateAsync_WhenFirstModelFailsWith429_RotatesToFallbackModel()
    {
        // Arrange
        var fakeStorage = new InMemorySettingsService();
        fakeStorage.Save(new AppSettings { LastWorkingModel = "models/gemini-3.6-flash" });

        var handler = new MockHttpMessageHandler();
        int callCount = 0;

        handler.Responder = req =>
        {
            callCount++;
            var uri = req.RequestUri?.ToString() ?? string.Empty;

            // Lần 1: Model 3.6 flash (Fast-path) trả về 429 Rate Limit
            if (callCount == 1 && uri.Contains("gemini-3.6-flash"))
            {
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("{\"error\":{\"code\":429,\"message\":\"Quota exceeded\"}}", Encoding.UTF8, "application/json")
                };
            }

            // Gọi API list models
            if (req.Method == HttpMethod.Get)
            {
                var discoveryPayload = new
                {
                    models = new[]
                    {
                        new { name = "models/gemini-3.6-flash", supportedGenerationMethods = new[] { "generateContent" } },
                        new { name = "models/gemini-3.5-flash", supportedGenerationMethods = new[] { "generateContent" } }
                    }
                };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(discoveryPayload), Encoding.UTF8, "application/json")
                };
            }

            // Lần thử tiếp theo: Model 3.5 flash thành công
            var successPayload = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = new[]
                            {
                                new { text = "Xin chào (dịch từ 3.5)" }
                            }
                        }
                    }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(successPayload), Encoding.UTF8, "application/json")
            };
        };

        var translator = new GoogleGeminiTranslator(new HttpClient(handler), fakeStorage);

        // Act
        var translated = await translator.TranslateAsync("Hello", "Vietnamese", "English", "fake-key");

        // Assert: Đã fallback thành công sang model khác và lưu model mới
        Assert.Equal("Xin chào (dịch từ 3.5)", translated);
        Assert.Equal("models/gemini-3.5-flash", fakeStorage.Load().LastWorkingModel);
    }
}
