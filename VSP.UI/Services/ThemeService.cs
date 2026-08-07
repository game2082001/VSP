using System.Windows;
using Microsoft.Win32;
using VSP.Core.Logging;
using VSP.Infrastructure.Settings;

namespace VSP.UI.Services;

/// <summary>
/// Owns every theme-selection decision -- resolving <see cref="AppTheme.System"/> against the
/// OS registry setting and swapping the active <c>Themes/Dark.xaml</c>/<c>Light.xaml</c> resource
/// dictionary -- so no other type (<c>App.xaml.cs</c>, a View, <c>SettingsViewModel</c>) needs to
/// know how a theme is chosen or applied. Static, hand-wired, no DI container, matching
/// <c>AppLog</c>/<c>RecordingPathProvider</c>'s existing convention. Never throws: a registry
/// read failure or a resource-dictionary swap failure both fall back to Dark and log a Warning
/// (Epic-016 §8).
/// </summary>
public static class ThemeService
{
    private static readonly Uri DarkThemeUri = new("Themes/Dark.xaml", UriKind.Relative);
    private static readonly Uri LightThemeUri = new("Themes/Light.xaml", UriKind.Relative);

    private const string PersonalizeKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValueName = "AppsUseLightTheme";

    /// <summary>
    /// Resolves <see cref="AppTheme.System"/> once (no live reaction to a later OS theme change,
    /// per Epic-016 TD-035) and swaps the App-level brush resource dictionary. Never throws.
    /// </summary>
    public static void Apply(AppTheme theme)
    {
        var resolvedTheme = theme == AppTheme.System ? ResolveSystemTheme() : theme;
        ApplyResourceDictionary(resolvedTheme);
    }

    internal static AppTheme ResolveSystemTheme() => ResolveSystemTheme(ReadAppsUseLightTheme);

    /// <summary>Test seam: <paramref name="readAppsUseLightTheme"/> replaces the real registry read.</summary>
    internal static AppTheme ResolveSystemTheme(Func<int?> readAppsUseLightTheme)
    {
        try
        {
            return readAppsUseLightTheme() == 1 ? AppTheme.Light : AppTheme.Dark;
        }
        catch (Exception ex)
        {
            AppLog.Warning("Could not read the Windows theme preference from the registry; using Dark.", ex);
            return AppTheme.Dark;
        }
    }

    private static int? ReadAppsUseLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
        return key?.GetValue(AppsUseLightThemeValueName) as int?;
    }

    private static void ApplyResourceDictionary(AppTheme theme)
    {
        try
        {
            SwapMergedDictionary(theme == AppTheme.Light ? LightThemeUri : DarkThemeUri);
        }
        catch (Exception ex)
        {
            AppLog.Warning($"Could not apply the {theme} theme; falling back to Dark.", ex);

            if (theme != AppTheme.Dark)
            {
                try
                {
                    SwapMergedDictionary(DarkThemeUri);
                }
                catch
                {
                    // Already logged the original failure above; a second failure applying the
                    // Dark fallback itself is left as-is rather than looping or logging twice.
                }
            }
        }
    }

    private static void SwapMergedDictionary(Uri themeUri)
    {
        var merged = Application.Current.Resources.MergedDictionaries;

        var existing = merged.FirstOrDefault(d => d.Source == DarkThemeUri || d.Source == LightThemeUri);
        if (existing is not null)
        {
            merged.Remove(existing);
        }

        merged.Add(new ResourceDictionary { Source = themeUri });
    }
}
