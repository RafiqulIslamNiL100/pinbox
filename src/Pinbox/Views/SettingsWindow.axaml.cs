using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Pinbox.Models;
using Pinbox.Services;

namespace Pinbox.Views;

public partial class SettingsWindow : Window
{
    public event EventHandler? SignOutRequested;
    public event EventHandler? HotkeysChanged;

    private readonly AuthSession _session;
    private AppSettings _settings;

    public SettingsWindow() : this(new AuthSession()) { }

    public SettingsWindow(AuthSession session)
    {
        InitializeComponent();
        _session = session;
        _settings = AppSettingsService.Load();

        HotkeyBox.Text = _settings.GlobalHotkey;
        AutoStartToggle.IsChecked = _settings.StartWithWindows;
        CompactToggle.IsChecked = _settings.CompactModeDefault;
        NotifToggle.IsChecked = _settings.NotificationsEnabled;
        switch (_settings.Theme)
        {
            case "Light": ThemeLightBtn.IsChecked = true; break;
            case "Dark": ThemeDarkBtn.IsChecked = true; break;
            default: ThemeSystemBtn.IsChecked = true; break;
        }

        AccountEmail.Text = session.Email;
        LicenseDesc.Text = "Managed by your activation key";

        LangEnBtn.IsChecked = Loc.Lang != "zh";
        LangZhBtn.IsChecked = Loc.Lang == "zh";
        switch (_settings.AutoLockMinutes)
        {
            case 5: AutoLock5Btn.IsChecked = true; break;
            case 15: AutoLock15Btn.IsChecked = true; break;
            default: AutoLockOffBtn.IsChecked = true; break;
        }
    }

    private void OnGeneralTabClick(object? sender, RoutedEventArgs e)
    {
        GeneralTabBtn.IsChecked = true;
        AccountTabBtn.IsChecked = false;
        GeneralPanel.IsVisible = true;
        AccountPanel.IsVisible = false;
    }

    private void OnAccountTabClick(object? sender, RoutedEventArgs e)
    {
        GeneralTabBtn.IsChecked = false;
        AccountTabBtn.IsChecked = true;
        GeneralPanel.IsVisible = false;
        AccountPanel.IsVisible = true;
    }

    private void OnSaveGeneral(object? sender, RoutedEventArgs e)
    {
        var newHotkey = (HotkeyBox.Text ?? "").Trim();
        var hotkeyChanged = newHotkey != _settings.GlobalHotkey;

        _settings.GlobalHotkey = newHotkey;
        _settings.StartWithWindows = AutoStartToggle.IsChecked == true;
        _settings.CompactModeDefault = CompactToggle.IsChecked == true;
        _settings.NotificationsEnabled = NotifToggle.IsChecked == true;
        _settings.Theme = ThemeDarkBtn.IsChecked == true ? "Dark" : ThemeLightBtn.IsChecked == true ? "Light" : "System";

        AppSettingsService.Save(_settings);
        AppSettingsService.SetStartWithWindows(_settings.StartWithWindows);
        ThemeService.Apply(_settings.Theme);

        if (hotkeyChanged) HotkeysChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnLangEnClick(object? sender, RoutedEventArgs e)
    {
        Loc.Lang = "en";
        LangEnBtn.IsChecked = true;
        LangZhBtn.IsChecked = false;
    }

    private void OnLangZhClick(object? sender, RoutedEventArgs e)
    {
        Loc.Lang = "zh";
        LangEnBtn.IsChecked = false;
        LangZhBtn.IsChecked = true;
    }

    private void OnSaveAccount(object? sender, RoutedEventArgs e)
    {
        _settings.AutoLockMinutes = AutoLock15Btn.IsChecked == true ? 15 : AutoLock5Btn.IsChecked == true ? 5 : 0;
        if (!string.IsNullOrWhiteSpace(PinBox.Text))
            AppSettingsService.SetPin(_settings, PinBox.Text.Trim());
        AppSettingsService.Save(_settings);
        HotkeysChanged?.Invoke(this, EventArgs.Empty); // reused to also refresh auto-lock config
    }

    private void OnSignOutClick(object? sender, RoutedEventArgs e)
    {
        AuthService.ClearSession();
        SignOutRequested?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Pinbox data",
            SuggestedFileName = "pinbox-backup.zip",
            FileTypeChoices = new[] { new FilePickerFileType("Zip archive") { Patterns = new[] { "*.zip" } } },
        });

        if (file != null)
        {
            try { ImportExportService.Export(_session.UserId, file.Path.LocalPath); }
            catch (AuthException) { /* nothing to export yet */ }
        }
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Pinbox data",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Zip archive") { Patterns = new[] { "*.zip" } } },
        });

        var first = files.FirstOrDefault();
        if (first != null)
        {
            ImportExportService.Import(_session.UserId, first.Path.LocalPath);
        }
    }
}
