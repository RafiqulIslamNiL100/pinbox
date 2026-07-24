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
        AutoStartCheck.IsChecked = _settings.StartWithWindows;
        CompactCheck.IsChecked = _settings.CompactModeDefault;
        NotifCheck.IsChecked = _settings.NotificationsEnabled;
        ThemeCombo.SelectedIndex = _settings.Theme switch { "Light" => 0, "Dark" => 1, _ => 2 };

        AccountEmail.Text = session.Email;
        LicenseDesc.Text = "Managed by your activation key";

        LangEnBtn.IsChecked = Loc.Lang != "zh";
        LangZhBtn.IsChecked = Loc.Lang == "zh";
        AutoLockCombo.SelectedIndex = _settings.AutoLockMinutes switch { 5 => 1, 15 => 2, _ => 0 };
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
        _settings.StartWithWindows = AutoStartCheck.IsChecked == true;
        _settings.CompactModeDefault = CompactCheck.IsChecked == true;
        _settings.NotificationsEnabled = NotifCheck.IsChecked == true;
        _settings.Theme = ThemeCombo.SelectedIndex switch { 0 => "Light", 1 => "Dark", _ => "System" };

        AppSettingsService.Save(_settings);
        AppSettingsService.SetStartWithWindows(_settings.StartWithWindows);

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
        _settings.AutoLockMinutes = AutoLockCombo.SelectedIndex switch { 1 => 5, 2 => 15, _ => 0 };
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
