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
            if (uri.Contains("models?key="))
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
