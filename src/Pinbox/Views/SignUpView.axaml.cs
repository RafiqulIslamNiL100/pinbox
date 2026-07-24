using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Pinbox.Models;
using Pinbox.Services;

namespace Pinbox.Views;

public partial class SignUpView : UserControl
{
    public event EventHandler<AuthSession>? SignedUp;
    public event EventHandler? GoToSignIn;

    public SignUpView()
    {
        InitializeComponent();
    }

    private async void OnSignUpClick(object? sender, RoutedEventArgs e)
    {
        ErrorBox.IsVisible = false;
        SignUpButton.IsEnabled = false;
        try
        {
            var session = await AuthService.SignUpAsync(NameBox.Text ?? "", EmailBox.Text ?? "", PasswordBox.Text ?? "");
            SignedUp?.Invoke(this, session);
        }
        catch (AuthException ex)
        {
            ErrorText.Text = ex.Message;
            ErrorBox.IsVisible = true;
        }
        finally
        {
            SignUpButton.IsEnabled = true;
        }
    }

    private void OnGoSignIn(object? sender, PointerPressedEventArgs e)
    {
        GoToSignIn?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        NameBox.Text = "";
        EmailBox.Text = "";
        PasswordBox.Text = "";
        ErrorBox.IsVisible = false;
    }
}
