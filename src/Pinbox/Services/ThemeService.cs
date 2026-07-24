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

    // FluentTheme's own built-in controls (a checked ToggleButton, a
    // focused TextBox's underline, selected text in any TextBox, etc.) don't
    // read our custom brushes above - they read these specific keys, which
    // Avalonia otherwise fills in from the *Windows* system accent color.
    // Left alone, anyone whose Windows accent color is red (or any non-purple
    // color) sees that color bleed through on every control we didn't
    // hand-restyle ourselves. Overriding these forces every native FluentTheme
    // control back onto our own palette regardless of the user's OS setting.
    private static readonly Dictionary<string, Color> DarkAccentRamp = new()
    {
        ["SystemAccentColor"] = Color.Parse("#A78BFA"),
        ["SystemAccentColorDark1"] = Color.Parse("#9678E8"),
        ["SystemAccentColorDark2"] = Color.Parse("#8566D1"),
        ["SystemAccentColorDark3"] = Color.Parse("#7454BA"),
        ["SystemAccentColorLight1"] = Color.Parse("#B79FFB"),
        ["SystemAccentColorLight2"] = Color.Parse("#C7B3FC"),
        ["SystemAccentColorLight3"] = Color.Parse("#D7C7FD"),
    };

    private static readonly Dictionary<string, Color> LightAccentRamp = new()
    {
        ["SystemAccentColor"] = Color.Parse("#7C5CD9"),
        ["SystemAccentColorDark1"] = Color.Parse("#6A4BC4"),
        ["SystemAccentColorDark2"] = Color.Parse("#583AAE"),
        ["SystemAccentColorDark3"] = Color.Parse("#472999"),
        ["SystemAccentColorLight1"] = Color.Parse("#8F6EE0"),
        ["SystemAccentColorLight2"] = Color.Parse("#A282E7"),
        ["SystemAccentColorLight3"] = Color.Parse("#B596ED"),
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

        // These are Color resources, not Brush ones - FluentTheme's own
        // styles build brushes out of them internally.
        var accentRamp = useLight ? LightAccentRamp : DarkAccentRamp;
        foreach (var (key, color) in accentRamp)
        {
            Application.Current.Resources[key] = color;
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
