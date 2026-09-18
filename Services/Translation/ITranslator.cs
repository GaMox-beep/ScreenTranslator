namespace ScreenTranslator.Services.Translation;

public interface ITranslator
{
    Task<string> TranslateAsync(
        string text,
        string targetLanguage,
        string sourceLanguage,
        string apiKey,
        CancellationToken cancellationToken = default);
}
