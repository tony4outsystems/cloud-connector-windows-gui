using System.Reflection;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CloudConnectorGui.Views;

public sealed partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var informationalVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = informationalVersion?.Split('+')[0];
        this.FindControl<TextBlock>("VersionText")!.Text = $"Version {version}";
        this.FindControl<Button>("OkButton")!.Click += (_, _) => Close();
    }

    public static Task ShowAsync(Window owner)
    {
        var dialog = new AboutWindow();
        return dialog.ShowDialog(owner);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
