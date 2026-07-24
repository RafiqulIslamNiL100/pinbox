using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Pinbox.Models;
using Pinbox.Services;

namespace Pinbox.Views;

public partial class SignInView : UserControl
{
    public event EventHandler<AuthSession>? SignedIn;
    public event EventHandler? GoToSignUp;

    public SignInView()
    {
        InitializeComponent();
    }

    private async void OnSignInClick(object? sender, RoutedEventArgs e)
    {
        ErrorBox.IsVisible = false;
        SignInButton.IsEnabled = false;
        try
        {
            var session = await AuthService.SignInAsync(EmailBox.Text ?? "", PasswordBox.Text ?? "");
            SignedIn?.Invoke(this, session);
        }
        catch (AuthException ex)
        {
            ErrorText.Text = ex.Message;
            ErrorBox.IsVisible = true;
        }
        finally
        {
            SignInButton.IsEnabled = true;
        }
    }

    private void OnGoSignUp(object? sender, PointerPressedEventArgs e)
    {
        GoToSignUp?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        EmailBox.Text = "";
        PasswordBox.Text = "";
        ErrorBox.IsVisible = false;
    }
}
