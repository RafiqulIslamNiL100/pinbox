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
        ApplyLocalization();
        Loc.LanguageChanged += ApplyLocalization;
    }

    private void ApplyLocalization()
    {
        TitleText.Text = Loc.T("create_account");
        LedeText.Text = Loc.T("signup_lede2");
        NameLabel.Text = Loc.T("name");
        NameBox.Watermark = Loc.T("name_watermark");
        EmailLabel.Text = Loc.T("email");
        EmailBox.Watermark = Loc.T("email_watermark");
        PasswordLabel.Text = Loc.T("password");
        PasswordBox.Watermark = Loc.T("password_watermark_new");
        SignUpButton.Content = Loc.T("create_account_btn");
        HaveAccountText.Text = Loc.T("already_have_account");
        GoSignIn.Text = Loc.T("sign_in");
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
