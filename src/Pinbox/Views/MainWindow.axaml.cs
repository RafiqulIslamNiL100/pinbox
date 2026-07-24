using System;
using System.IO;
using System.Text.Json;
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

    private static string SessionFilePath =>
        Path.Combine(AppPaths.DataDirectory, "session.json");

    public MainWindow()
    {
        InitializeComponent();

        SignInViewControl.SignedIn += (_, user) => EnterApp(user);
        SignInViewControl.GoToSignUp += (_, _) => ShowScreen("signup");

        SignUpViewControl.SignedUp += (_, user) => EnterApp(user);
        SignUpViewControl.GoToSignIn += (_, _) => ShowScreen("signin");

        MainViewControl.Initialize(this);
        MainViewControl.SignedOut += (_, _) =>
        {
            ClearSession();
            SignInViewControl.Clear();
            ShowScreen("signin");
        };

        Opened += async (_, _) =>
        {
            TryResumeSession();
            await CheckForUpdateAsync();
        };
    }

    private void ShowScreen(string name)
    {
        SignInViewControl.IsVisible = name == "signin";
        SignUpViewControl.IsVisible = name == "signup";
        MainViewControl.IsVisible = name == "main";
    }

    private void EnterApp(PublicUser user)
    {
        SaveSession(user);
        MainViewControl.EnterAs(user);
        ShowScreen("main");
    }

    private void TryResumeSession()
    {
        try
        {
            if (!File.Exists(SessionFilePath)) { ShowScreen("signin"); return; }
            var json = File.ReadAllText(SessionFilePath);
            var user = JsonSerializer.Deserialize<PublicUser>(json);
            if (user is not null)
            {
                MainViewControl.EnterAs(user);
                ShowScreen("main");
                return;
            }
        }
        catch { /* fall through to sign-in */ }
        ShowScreen("signin");
    }

    private void SaveSession(PublicUser user)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            File.WriteAllText(SessionFilePath, JsonSerializer.Serialize(user));
        }
        catch { /* non-fatal */ }
    }

    private void ClearSession()
    {
        try { if (File.Exists(SessionFilePath)) File.Delete(SessionFilePath); }
        catch { /* non-fatal */ }
    }

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
