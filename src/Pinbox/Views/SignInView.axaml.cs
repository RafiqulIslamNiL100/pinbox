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
        ApplyLocalization();
        Loc.LanguageChanged += ApplyLocalization;
    }

    private void ApplyLocalization()
    {
        TitleText.Text = Loc.T("welcome_back");
        LedeText.Text = Loc.T("signin_lede2");
        EmailLabel.Text = Loc.T("email");
        EmailBox.Watermark = Loc.T("email_watermark");
        PasswordLabel.Text = Loc.T("password");
        PasswordBox.Watermark = Loc.T("password_watermark");
        SignInButton.Content = Loc.T("sign_in");
        NoAccountText.Text = Loc.T("dont_have_account");
        GoSignUp.Text = Loc.T("sign_up");
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

    // Used to explain a local sign-out the user didn't ask for right now
    // (e.g. another device just claimed this account's single device slot).
    public void ShowMessage(string text)
    {
        ErrorText.Text = text;
        ErrorBox.IsVisible = true;
    }
}
