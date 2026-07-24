using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Pinbox.Models;
using Pinbox.Services;

namespace Pinbox.Views;

public partial class SignInView : UserControl
{
    public event EventHandler<PublicUser>? SignedIn;
    public event EventHandler? GoToSignUp;

    public SignInView()
    {
        InitializeComponent();
    }

    private void OnSignInClick(object? sender, RoutedEventArgs e)
    {
        ErrorBox.IsVisible = false;
        try
        {
            var user = AuthService.SignIn(EmailBox.Text ?? "", PasswordBox.Text ?? "");
            SignedIn?.Invoke(this, user);
        }
        catch (AuthException ex)
        {
            ErrorText.Text = ex.Message;
            ErrorBox.IsVisible = true;
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
