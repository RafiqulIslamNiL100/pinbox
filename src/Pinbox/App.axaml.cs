using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Pinbox.Services;
using Pinbox.Views;

namespace Pinbox;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private TrayIcon? _trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            ThemeService.Apply(AppSettingsService.Load().Theme);
            _mainWindow = new MainWindow();
            desktop.MainWindow = _mainWindow;
            _mainWindow.Show();

            SetUpTrayIcon(desktop);
        }
        base.OnFrameworkInitializationCompleted();
    }

    // The tray icon is a convenience, not essential — never let it crash the app.
    private void SetUpTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://Pinbox/Assets/icon.png"));
            var bitmap = new Bitmap(stream);

            var openItem = new NativeMenuItem("Open Pinbox");
            openItem.Click += (_, _) =>
            {
                _mainWindow?.Show();
                _mainWindow?.Activate();
            };

            var quitItem = new NativeMenuItem("Quit");
            quitItem.Click += (_, _) =>
            {
                if (_mainWindow is not null) _mainWindow.IsQuitting = true;
                desktop.Shutdown();
            };

            var menu = new NativeMenu();
            menu.Items.Add(openItem);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(quitItem);

            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(bitmap),
                ToolTipText = "Pinbox",
                Menu = menu,
            };
            _trayIcon.Clicked += (_, _) =>
            {
                _mainWindow?.Show();
                _mainWindow?.Activate();
            };

            TrayIcon.SetIcons(this, new TrayIcons { _trayIcon });
        }
        catch
        {
            // No usable icon or platform tray support; skip the tray rather than crash.
        }
    }
}
