using System;
using System.Collections.Generic;
using Avalonia;

namespace Pinbox;

class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions
            {
                // Software rendering avoids GPU/driver-related black-window
                // issues on machines with unusual or missing GPU drivers —
                // a small performance trade-off worth it for a simple UI.
                RenderingMode = new List<Win32RenderingMode> { Win32RenderingMode.Software },
            })
            .WithInterFont()
            .LogToTrace();
}
