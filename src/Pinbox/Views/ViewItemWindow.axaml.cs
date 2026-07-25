using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Pinbox.Views;

public partial class ViewItemWindow : Window
{
    public ViewItemWindow() : this("", "") { }

    public ViewItemWindow(string subject, string body)
    {
        InitializeComponent();
        SubjectText.Text = subject;
        BodyText.Text = body;
        CloseBtn.Content = Pinbox.Services.Loc.T("close");
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
