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
            // Note on Chinese: Inter (the default face) has no CJK glyphs, but
            // on Windows the platform font manager (DirectWrite) automatically
            // falls back to a system Chinese font (Microsoft YaHei) for any
            // glyph Inter lacks, so the translated 中文 interface renders
            // correctly with no extra configuration. An explicit
            // FontManagerOptions.FontFallbacks was tried and removed: it
            // pushed CJK rasterization down a path that hung under this
            // project's forced software-rendering on non-Windows test hosts,
            // while adding nothing on real Windows where DirectWrite already
            // handles the fallback.
            .LogToTrace();
}
