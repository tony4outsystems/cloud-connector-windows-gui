using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CloudConnectorGui.Views;

public sealed partial class MessageDialogWindow : Window
{
    public MessageDialogWindow()
    {
        InitializeComponent();
    }

    public MessageDialogWindow(string title, string message)
        : this()
    {
        Title = title;
        this.FindControl<TextBlock>("MessageText")!.Text = message;
        this.FindControl<Button>("OkButton")!.Click += (_, _) => Close();
    }

    public static Task ShowAsync(Window? owner, string title, string message)
    {
        var dialog = new MessageDialogWindow(title, message);
        return owner is not null ? dialog.ShowDialog(owner) : ShowStandaloneAsync(dialog);
    }

    private static Task ShowStandaloneAsync(Window dialog)
    {
        var completionSource = new TaskCompletionSource();
        dialog.Closed += (_, _) => completionSource.TrySetResult();
        dialog.Show();
        return completionSource.Task;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
