using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CloudConnectorGui.ViewModels;
using CloudConnectorGui.Views;
#if DEBUG
using AvaloniaUI.DiagnosticsSupport;
#endif

namespace CloudConnectorGui;

public sealed class GuiApplication : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };
            viewModel.OwnerWindow = window;
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void Preferences_OnClick(object? sender, System.EventArgs args)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: MainWindow { DataContext: MainWindowViewModel viewModel } })
        {
            viewModel.OpenConfigurationCommand.Execute(null);
        }
    }

    private void About_OnClick(object? sender, System.EventArgs args)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: MainWindow { DataContext: MainWindowViewModel viewModel } })
        {
            viewModel.OpenAboutCommand.Execute(null);
        }
    }
}
