using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;

namespace Pinbox.Services;

// Applies the user's Light/Dark/System choice by directly overwriting every
// brush in Application.Resources (a flat dictionary, not a
// ResourceDictionary.ThemeDictionaries pair - see the comment in App.axaml
// for why: code-behind resource lookups don't reliably resolve resources
// nested inside ThemeDictionaries, which made every brush built in C# code
// silently null). Overwriting Application.Resources[key] is what both XAML
// DynamicResource bindings and code-behind lookups actually react to live.
public static class ThemeService
{
    private static readonly Dictionary<string, Color> Dark = new()
    {
        ["BgBrush"] = Color.Parse("#14121D"),
        ["SurfaceBrush"] = Color.Parse("#1C1928"),
        ["Surface2Brush"] = Color.Parse("#262234"),
        ["Surface3Brush"] = Color.Parse("#2B2538"),
        ["BorderBrush2"] = Color.Parse("#383150"),
        ["BorderSoftBrush"] = Color.Parse("#2A2536"),
        ["TextBrush"] = Color.Parse("#ECE9F6"),
        ["TextDimBrush"] = Color.Parse("#A59EBD"),
        ["TextFaintBrush"] = Color.Parse("#6C6580"),
        ["AccentBrush"] = Color.Parse("#A78BFA"),
        ["AccentInkBrush"] = Color.Parse("#1C0F3E"),
        ["AccentSoftBrush"] = Color.Parse("#322A4D"),
        ["MauveBrush"] = Color.Parse("#D99BD0"),
        ["MauveSoftBrush"] = Color.Parse("#35243A"),
        ["GoldBrush"] = Color.Parse("#E0B23F"),
        ["GoldSoftBrush"] = Color.Parse("#3A2F14"),
        ["GoodBrush"] = Color.Parse("#4FD39A"),
        ["GoodSoftBrush"] = Color.Parse("#173A2C"),
    };

    private static readonly Dictionary<string, Color> Light = new()
    {
        ["BgBrush"] = Color.Parse("#F6F3FB"),
        ["SurfaceBrush"] = Color.Parse("#FFFFFF"),
        ["Surface2Brush"] = Color.Parse("#EFE9F9"),
        ["Surface3Brush"] = Color.Parse("#E4DAF5"),
        ["BorderBrush2"] = Color.Parse("#DDD2EF"),
        ["BorderSoftBrush"] = Color.Parse("#E7E0F5"),
        ["TextBrush"] = Color.Parse("#251F38"),
        ["TextDimBrush"] = Color.Parse("#6D6484"),
        ["TextFaintBrush"] = Color.Parse("#A297B8"),
        ["AccentBrush"] = Color.Parse("#7C5CD9"),
        ["AccentInkBrush"] = Color.Parse("#FFFFFF"),
        ["AccentSoftBrush"] = Color.Parse("#ECE2FB"),
        ["MauveBrush"] = Color.Parse("#A1478C"),
        ["MauveSoftBrush"] = Color.Parse("#F8E7F3"),
        ["GoldBrush"] = Color.Parse("#A3760A"),
        ["GoldSoftBrush"] = Color.Parse("#FAF0D6"),
        ["GoodBrush"] = Color.Parse("#1F7A4C"),
        ["GoodSoftBrush"] = Color.Parse("#D9F2E6"),
    };

    public static void Apply(string theme)
    {
        if (Application.Current is null) return;

        bool useLight = theme switch
        {
            "Light" => true,
            "Dark" => false,
            _ => IsSystemLight(),
        };

        Application.Current.RequestedThemeVariant = useLight ? ThemeVariant.Light : ThemeVariant.Dark;

        var palette = useLight ? Light : Dark;
        foreach (var (key, color) in palette)
        {
            Application.Current.Resources[key] = new SolidColorBrush(color);
        }
    }

    private static bool IsSystemLight()
    {
        try
        {
            var variant = Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant;
            return variant == PlatformThemeVariant.Light;
        }
        catch
        {
            return false; // couldn't detect - default to dark
        }
    }
}
