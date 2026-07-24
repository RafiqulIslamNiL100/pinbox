using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Pinbox.Models;
using Pinbox.Services;

namespace Pinbox.Views;

public partial class SignUpView : UserControl
{
    public event EventHandler<PublicUser>? SignedUp;
    public event EventHandler? GoToSignIn;

    public SignUpView()
    {
        InitializeComponent();
    }

    private void OnSignUpClick(object? sender, RoutedEventArgs e)
    {
        ErrorBox.IsVisible = false;
        try
        {
            var user = AuthService.SignUp(NameBox.Text ?? "", EmailBox.Text ?? "", PasswordBox.Text ?? "");
            SignedUp?.Invoke(this, user);
        }
        catch (AuthException ex)
        {
            ErrorText.Text = ex.Message;
            ErrorBox.IsVisible = true;
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
