using System.Runtime.InteropServices;
using Avalonia;

namespace CloudConnectorGui;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\CloudConnectorGui";

    [STAThread]
    public static void Main(string[] args)
    {
        using var singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBoxW(IntPtr.Zero, "Cloud Connector GUI is already running.", "Already running", MB_OK | MB_ICONINFORMATION);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<GuiApplication>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }

    private const uint MB_OK = 0x0;
    private const uint MB_ICONINFORMATION = 0x40;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
