using System.Windows;

namespace ScreenTranslator.Services.Orchestration;

public record TranslationPipelineRequest(
    Rect SelectedArea,
    double DpiScaleX,
    double DpiScaleY,
    string SourceLanguage,
    string TargetLanguage,
    string ApiKey);

public record TranslationPipelineCallbacks(
    Action OnLoading,
    Action<string> OnSuccess,
    Action<string> OnError,
    Action<string, bool> OnStatusChanged);

public interface ITranslationOrchestrator
{
    Task ProcessAsync(
        TranslationPipelineRequest request,
        TranslationPipelineCallbacks callbacks,
        CancellationToken cancellationToken = default);
}
