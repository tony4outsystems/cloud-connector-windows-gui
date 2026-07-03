using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CloudConnectorGui.ViewModels;

namespace CloudConnectorGui.Views;

public sealed partial class ConfigurationWindow : Window
{
    public ConfigurationWindow()
    {
        InitializeComponent();
    }

    public static Task ShowAsync(Window owner, MainWindowViewModel viewModel)
    {
        var dialog = new ConfigurationWindow { DataContext = viewModel };
        return dialog.ShowDialog(owner);
    }

    private void Close_OnClick(object? sender, RoutedEventArgs args)
    {
        Close();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
