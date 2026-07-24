using System;
using Avalonia;
using Avalonia.Styling;

namespace Pinbox.Services;

// Drives the app's Light/Dark/System choice through Avalonia's native theme
// variant system. The brush palettes for each theme live in App.axaml's
// ResourceDictionary.ThemeDictionaries, so setting RequestedThemeVariant is
// all that's needed - Avalonia re-resolves every {DynamicResource ...} in
// every open window automatically, at startup and on a live switch, for the
// main window and dialogs alike. Code-behind that builds visuals in C# reads
// the same values with a theme-aware TryFindResource (see MainView.Brush),
// and re-renders on the ThemeChanged event below.
public static class ThemeService
{
    public static event Action? ThemeChanged;

    public static void Apply(string theme)
    {
        if (Application.Current is null) return;

        Application.Current.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default, // System - follows the OS setting
        };

        ThemeChanged?.Invoke();
    }
}
