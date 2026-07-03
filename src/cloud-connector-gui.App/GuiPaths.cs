using System.Reflection;

namespace CloudConnectorGui.App;

public static class GuiPaths
{
    public static string GetAppDataDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var productName = Assembly.GetExecutingAssembly().GetName().Name ?? "cloud-connector-gui";
        var directory = Path.Combine(localAppData, productName);
        Directory.CreateDirectory(directory);
        return directory;
    }
}
