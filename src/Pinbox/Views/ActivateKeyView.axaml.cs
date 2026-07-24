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

    public ActivateKeyView()
    {
        InitializeComponent();
    }

    public void SetSession(AuthSession session, string? reason = null)
    {
        _session = session;
        ErrorBox.IsVisible = false;
        KeyBox.Text = "";

        if (reason == "expired")
        {
            TitleText.Text = "Your key has expired";
            LedeText.Text = "Enter a new key to keep using Pinbox.";
        }
        else if (reason == "banned" || reason == "restricted")
        {
            TitleText.Text = "Access unavailable";
            LedeText.Text = LicenseService.DescribeReason(reason, Loc.Lang == "zh");
        }
        else
        {
            TitleText.Text = "Enter your key";
            LedeText.Text = "Pinbox is locked until you activate it with a key.";
        }
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
                ErrorText.Text = "Enter a key first.";
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
