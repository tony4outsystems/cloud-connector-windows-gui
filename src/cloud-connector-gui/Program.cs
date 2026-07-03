using Avalonia;
using Velopack;

namespace CloudConnectorGui;

internal static class Program
{
    private const string SingleInstanceMutexName = "CloudConnectorGui-SingleInstance";

    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        using var singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        GuiApplication.AlreadyRunning = !createdNew;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<GuiApplication>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
