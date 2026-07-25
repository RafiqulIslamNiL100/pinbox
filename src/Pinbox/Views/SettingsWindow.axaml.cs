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

        LangEnBtn.IsChecked = Loc.Lang != "zh";
        LangZhBtn.IsChecked = Loc.Lang == "zh";
        switch (_settings.AutoLockMinutes)
        {
            case 5: AutoLock5Btn.IsChecked = true; break;
            case 15: AutoLock15Btn.IsChecked = true; break;
            default: AutoLockOffBtn.IsChecked = true; break;
        }

        ApplyLocalization();
        Loc.LanguageChanged += ApplyLocalization;
        Closed += (_, _) => Loc.LanguageChanged -= ApplyLocalization;
    }

    private void ApplyLocalization()
    {
        TitleText.Text = Loc.T("settings");
        GeneralTabBtn.Content = Loc.T("general");
        AccountTabBtn.Content = Loc.T("account_data");

        HotkeyLabel.Text = Loc.T("global_hotkey");
        HotkeyDesc.Text = Loc.T("desc_hotkey");
        StartupLabel.Text = Loc.T("start_with_windows");
        StartupDesc.Text = Loc.T("desc_startup");
        CompactLabel.Text = Loc.T("compact_mode");
        CompactDesc.Text = Loc.T("desc_compact");
        NotifLabel.Text = Loc.T("notifications");
        NotifDesc.Text = Loc.T("desc_notifications");
        ThemeLabel.Text = Loc.T("theme");
        ThemeDesc.Text = Loc.T("desc_theme");
        ThemeLightBtn.Content = Loc.T("light");
        ThemeDarkBtn.Content = Loc.T("dark");
        ThemeSystemBtn.Content = Loc.T("system");
        SaveGeneralBtn.Content = Loc.T("save");

        LicenseLabel.Text = Loc.T("license");
        LicenseDesc.Text = Loc.T("desc_license");
        LicenseBadge.Text = Loc.T("active");
        AccountLabel.Text = Loc.T("account");
        SignOutBtn.Content = Loc.T("sign_out");
        LanguageLabel.Text = Loc.T("language");
        LanguageDesc.Text = Loc.T("desc_language");
        AutoLockLabel.Text = Loc.T("auto_lock");
        AutoLockDesc.Text = Loc.T("desc_autolock");
        AutoLockOffBtn.Content = Loc.T("off");
        AutoLock5Btn.Content = Loc.T("autolock_5");
        AutoLock15Btn.Content = Loc.T("autolock_15");
        SetPinLabel.Text = Loc.T("set_pin");
        SetPinDesc.Text = Loc.T("desc_setpin");
        BackupLabel.Text = Loc.T("backup");
        BackupDesc.Text = Loc.T("desc_backup");
        ExportBtn.Content = Loc.T("export");
        ImportBtn.Content = Loc.T("import");
        SaveAccountBtn.Content = Loc.T("save");
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

    private string SelectedTheme =>
        ThemeDarkBtn.IsChecked == true ? "Dark" : ThemeLightBtn.IsChecked == true ? "Light" : "System";

    // Apply the theme the instant a Light/Dark/System pill is clicked, so the
    // whole app switches live - the user shouldn't have to press Save to see
    // it. Save still persists it so the choice survives a restart.
    private void OnThemeClick(object? sender, RoutedEventArgs e)
    {
        ThemeService.Apply(SelectedTheme);
    }

    private void OnSaveGeneral(object? sender, RoutedEventArgs e)
    {
        var newHotkey = (HotkeyBox.Text ?? "").Trim();
        var hotkeyChanged = newHotkey != _settings.GlobalHotkey;

        _settings.GlobalHotkey = newHotkey;
        _settings.StartWithWindows = AutoStartToggle.IsChecked == true;
        _settings.CompactModeDefault = CompactToggle.IsChecked == true;
        _settings.NotificationsEnabled = NotifToggle.IsChecked == true;
        _settings.Theme = SelectedTheme;

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
