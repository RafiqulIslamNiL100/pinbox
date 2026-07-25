using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Pinbox.Models;
using Pinbox.Services;

namespace Pinbox.Views;

public partial class PinLockWindow : Window
{
    private readonly AppSettings _settings;

    public PinLockWindow() : this(new AppSettings()) { }

    public PinLockWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        PinTitle.Text = Loc.T("enter_pin");
        PinLede.Text = Loc.T("locked_after_idle");
        UnlockBtn.Content = Loc.T("unlock");
        Opened += (_, _) => PinInput.Focus();
    }

    private void TryUnlock()
    {
        if (AppSettingsService.VerifyPin(_settings, PinInput.Text ?? ""))
        {
            Close(true);
        }
        else
        {
            ErrorText.Text = "Incorrect PIN.";
            ErrorText.IsVisible = true;
            PinInput.Text = "";
        }
    }

    private void OnUnlockClick(object? sender, RoutedEventArgs e) => TryUnlock();
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) TryUnlock();
    }
}
