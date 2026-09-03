namespace ScreenTranslator.Models;

public class ProviderInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public List<string> Models { get; set; } = new();
    public bool RequiresCustomUrl { get; set; }

    public override string ToString() => Name;
}
