using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Pinbox.Models;
using Pinbox.Services;

namespace Pinbox.Views;

public partial class MainWindow : Window
{
    public bool IsQuitting { get; set; }

    private UpdateInfo? _pendingUpdate;
    private AuthSession? _session;
    private HotkeyService? _hotkeys;
    private AutoLockService? _autoLock;
    private DispatcherTimer? _licenseTimer;
    private DispatcherTimer? _foregroundTracker;

    public MainWindow()
    {
        InitializeComponent();

        SignInViewControl.SignedIn += async (_, session) => await OnAuthenticated(session);
        SignInViewControl.GoToSignUp += (_, _) => ShowScreen("signup");

        SignUpViewControl.SignedUp += async (_, session) => await OnAuthenticated(session);
        SignUpViewControl.GoToSignIn += (_, _) => ShowScreen("signin");

        ActivateKeyViewControl.Activated += async (_, _) => await EnterMainAsync();
        ActivateKeyViewControl.SignOutRequested += (_, _) => DoSignOut();

        MainViewControl.Initialize(this);
        MainViewControl.SignedOut += (_, _) => DoSignOut();
        MainViewControl.PageHotkeysChanged += (_, _) => RegisterHotkeys();

        Opened += async (_, _) =>
        {
            SetUpHotkeysAndAutoLock();
            await TryResumeSessionAsync();
            await CheckForUpdateAsync();
        };
    }

    private void ShowScreen(string name)
    {
        SignInViewControl.IsVisible = name == "signin";
        SignUpViewControl.IsVisible = name == "signup";
        ActivateKeyViewControl.IsVisible = name == "activate";
        MainViewControl.IsVisible = name == "main";
    }

    private async Task OnAuthenticated(AuthSession session)
    {
        _session = session;
        AuthService.SaveSession(session);

        // An explicit sign-in/sign-up claims this device as the account's
        // single active device, signing out any other device using it.
        var status = await SafeCheckLicenseAsync(claim: true);
        if (status is { Ok: true })
        {
            await EnterMainAsync();
        }
        else if (status is { Reason: "device_mismatch" })
        {
            // Only possible if another device claimed the slot in the
            // instant between this device's claim attempt and now - treat
            // it the same as any other device takeover.
            HandleDeviceMismatch();
        }
        else
        {
            ActivateKeyViewControl.SetSession(session, status?.Reason);
            ShowScreen("activate");
        }
    }

    private async Task<LicenseStatus?> SafeCheckLicenseAsync(bool claim = false)
    {
        if (_session is null) return null;
        try
        {
            return await LicenseService.CheckLicenseAsync(_session, claim);
        }
        catch
        {
            // Network unreachable etc. - treat as "can't verify right now"
            // rather than instantly locking someone out over a dropped
            // connection. Caller decides what to do with a null result.
            return null;
        }
    }

    private async Task EnterMainAsync()
    {
        if (_session is null) return;
        MainViewControl.EnterAs(_session);
        ShowScreen("main");
        RegisterHotkeys();
        StartLicensePolling();
    }

    private async Task TryResumeSessionAsync()
    {
        var saved = AuthService.LoadSession();
        if (saved is null)
        {
            ShowScreen("signin");
            return;
        }

        _session = saved;
        // A resumed session never claims the device - it only confirms
        // this device still holds the account's single active slot.
        var status = await SafeCheckLicenseAsync();

        if (status is null)
        {
            // Couldn't reach the server right now - let them in with what we
            // last knew rather than blocking on a network hiccup.
            await EnterMainAsync();
        }
        else if (status.Ok)
        {
            await EnterMainAsync();
        }
        else if (status.Reason == "device_mismatch")
        {
            HandleDeviceMismatch();
        }
        else
        {
            ActivateKeyViewControl.SetSession(_session, status.Reason);
            ShowScreen("activate");
        }
    }

    private void StartLicensePolling()
    {
        _licenseTimer?.Stop();
        _licenseTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _licenseTimer.Tick += async (_, _) =>
        {
            var status = await SafeCheckLicenseAsync();
            if (status is { Ok: false } && _session is not null)
            {
                _licenseTimer?.Stop();
                if (status.Reason == "device_mismatch")
                    HandleDeviceMismatch();
                else
                {
                    ActivateKeyViewControl.SetSession(_session, status.Reason);
                    ShowScreen("activate");
                }
            }
        };
        _licenseTimer.Start();
    }

    // Another device has taken over the account's single device slot -
    // sign out locally (this device never had it taken by force, it just
    // stops being allowed to use it) and explain why, so the user isn't
    // left wondering why they were dropped back to the sign-in screen.
    private void HandleDeviceMismatch()
    {
        DoSignOut();
        SignInViewControl.ShowMessage(Loc.T("signed_out_other_device"));
    }

    private void DoSignOut()
    {
        _licenseTimer?.Stop();
        _session = null;
        AuthService.ClearSession();
        SignInViewControl.Clear();
        ShowScreen("signin");
    }

    // ---------------- hotkeys ----------------

    private void SetUpHotkeysAndAutoLock()
    {
        _hotkeys = new HotkeyService();
        _autoLock = new AutoLockService();
        _autoLock.IdleTimeoutReached += async () => await ShowPinLockAsync();

        var settings = AppSettingsService.Load();
        _autoLock.Configure(settings.PinHash != null ? settings.AutoLockMinutes : 0);

        StartForegroundTracking();
    }

    // Continuously remember the last window the user was focused on that
    // ISN'T Pinbox. When they click a saved item to send it, PasteService
    // switches focus back to this window and pastes there - which is how
    // Pinbox can send into the app you were just using without hiding itself.
    private void StartForegroundTracking()
    {
        if (!OperatingSystem.IsWindows()) return;
        _foregroundTracker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _foregroundTracker.Tick += (_, _) =>
        {
            var mine = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            var fg = PasteService.CurrentForegroundWindow();
            if (fg != IntPtr.Zero && fg != mine)
                PasteService.LastExternalWindow = fg;
        };
        _foregroundTracker.Start();
    }

    private void RegisterHotkeys()
    {
        if (_hotkeys is null) return;
        _hotkeys.UnregisterAll();

        var settings = AppSettingsService.Load();
        _hotkeys.Register(settings.GlobalHotkey, () =>
        {
            Dispatcher.UIThread.Post(() => { Show(); Activate(); });
        });

        foreach (var page in MainViewControl.Pages)
        {
            if (string.IsNullOrWhiteSpace(page.Hotkey)) continue;
            var pageId = page.Id;
            _hotkeys.Register(page.Hotkey!, () =>
            {
                Dispatcher.UIThread.Post(() => { Show(); Activate(); });
            });
        }

        _autoLock?.Configure(settings.PinHash != null ? settings.AutoLockMinutes : 0);
    }

    private async Task ShowPinLockAsync()
    {
        var settings = AppSettingsService.Load();
        if (string.IsNullOrEmpty(settings.PinHash)) return;

        Hide();
        var dlg = new PinLockWindow(settings);
        var unlocked = await dlg.ShowDialog<bool>(this);
        while (!unlocked)
        {
            dlg = new PinLockWindow(settings);
            unlocked = await dlg.ShowDialog<bool>(this);
        }
        _autoLock?.NotifyUnlocked();
        Show();
        Activate();
    }

    // ---------------- update banner (unchanged behavior) ----------------

    private async Task CheckForUpdateAsync()
    {
        var info = await UpdateService.CheckForUpdateAsync();
        if (!info.Available || info.DownloadUrl is null) return;

        _pendingUpdate = info;
        UpdateBannerText.Text = $"Pinbox {info.Version} is available.";
        UpdateBanner.IsVisible = true;
    }

    private async void OnUpdateNow(object? sender, RoutedEventArgs e)
    {
        if (_pendingUpdate?.DownloadUrl is null) return;
        UpdateNowButton.IsEnabled = false;
        UpdateNowButton.Content = "Updating…";
        try
        {
            await UpdateService.DownloadAndApplyAsync(_pendingUpdate.DownloadUrl);
            IsQuitting = true;
            await Task.Delay(200);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            UpdateNowButton.IsEnabled = true;
            UpdateNowButton.Content = "Update now";
            UpdateBannerText.Text = $"Update failed: {ex.Message}";
        }
    }

    private void OnDismissUpdate(object? sender, PointerPressedEventArgs e)
    {
        UpdateBanner.IsVisible = false;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (IsQuitting) return;
        e.Cancel = true;
        Hide();
    }
}
