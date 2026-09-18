using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ScreenTranslator.Services;
using ScreenTranslator.Services.Capture;
using ScreenTranslator.Services.Ocr;
using ScreenTranslator.Services.Orchestration;
using ScreenTranslator.Services.Storage;
using ScreenTranslator.Services.Translation;
using ScreenTranslator.ViewModels;

namespace ScreenTranslator;

public partial class App : Application
{
    private const int AttachParentProcess = -1;

    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int dwProcessId);

    protected override void OnStartup(StartupEventArgs e)
    {
        // Gắn vào cửa sổ Terminal/PowerShell của tiến trình cha để in log trực tiếp
        if (AttachConsole(AttachParentProcess))
        {
            var stdout = Console.OpenStandardOutput();
            var writer = new System.IO.StreamWriter(stdout, System.Text.Encoding.UTF8) { AutoFlush = true };
            Console.SetOut(writer);
            Console.SetError(writer);
        }

        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        AppLogger.Log("[INIT] Application startup initialized with Dependency Injection container.");
        base.OnStartup(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Storage & Infrastructure
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IScreenCaptureService, WindowsScreenCaptureService>();
        services.AddSingleton<IOcrEngine, WindowsMediaOcr>();
        services.AddSingleton<ITranslator, GoogleGeminiTranslator>();

        // Deep Module Orchestrator
        services.AddSingleton<ITranslationOrchestrator, TranslationOrchestrator>();

        // ViewModels & Presentation
        services.AddTransient<MainViewModel>();
    }
}
