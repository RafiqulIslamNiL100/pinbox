using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Pinbox.Services;

namespace Pinbox.Views;

public partial class PromptWindow : Window
{
    public PromptWindow() : this("", "") { }

    public PromptWindow(string label, string initialValue)
    {
        InitializeComponent();
        PromptLabel.Text = label;
        InputBox.Text = initialValue;
        CancelBtn.Content = Loc.T("cancel");
        OkBtn.Content = Loc.T("ok");
        Opened += (_, _) => InputBox.Focus();
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close(InputBox.Text);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Close(InputBox.Text);
        else if (e.Key == Key.Escape) Close(null);
    }
}
