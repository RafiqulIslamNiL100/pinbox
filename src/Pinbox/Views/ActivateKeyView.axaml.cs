using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Pinbox.Models;
using Pinbox.Services;

namespace Pinbox.Views;

public partial class ActivateKeyView : UserControl
{
    public event EventHandler? Activated;
    public event EventHandler? SignOutRequested;

    private AuthSession? _session;

    private string? _reason;

    public ActivateKeyView()
    {
        InitializeComponent();
        ApplyLocalization();
        Loc.LanguageChanged += ApplyLocalization;
    }

    private void ApplyLocalization()
    {
        KeyLabel.Text = Loc.T("unique_key");
        KeyBox.Watermark = Loc.T("key_watermark");
        ActivateButton.Content = Loc.T("activate");
        SignOutLink.Text = Loc.T("sign_out");
        RefreshHeading();
    }

    private void RefreshHeading()
    {
        if (_reason == "expired")
        {
            TitleText.Text = Loc.T("key_expired_title");
            LedeText.Text = Loc.T("key_expired_lede");
        }
        else if (_reason == "banned" || _reason == "restricted")
        {
            TitleText.Text = Loc.T("access_unavailable");
            LedeText.Text = LicenseService.DescribeReason(_reason, Loc.Lang == "zh");
        }
        else
        {
            TitleText.Text = Loc.T("enter_key");
            LedeText.Text = Loc.T("activate_lede");
        }
    }

    public void SetSession(AuthSession session, string? reason = null)
    {
        _session = session;
        _reason = reason;
        ErrorBox.IsVisible = false;
        KeyBox.Text = "";
        RefreshHeading();
    }

    private async void OnActivateClick(object? sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        ErrorBox.IsVisible = false;
        ActivateButton.IsEnabled = false;
        try
        {
            var code = (KeyBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(code))
            {
                ErrorText.Text = Loc.T("enter_key_first");
                ErrorBox.IsVisible = true;
                return;
            }

            var result = await LicenseService.ActivateKeyAsync(_session, code);
            if (result.Ok)
            {
                Activated?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ErrorText.Text = LicenseService.DescribeReason(result.Reason, Loc.Lang == "zh");
                ErrorBox.IsVisible = true;
            }
        }
        catch (AuthException ex)
        {
            ErrorText.Text = ex.Message;
            ErrorBox.IsVisible = true;
        }
        finally
        {
            ActivateButton.IsEnabled = true;
        }
    }

    private void OnSignOutClick(object? sender, PointerPressedEventArgs e)
    {
        SignOutRequested?.Invoke(this, EventArgs.Empty);
    }
}
