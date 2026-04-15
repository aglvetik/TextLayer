using Microsoft.Win32;
using System.Windows;
using TextLayer.Application.Models;

namespace TextLayer.App.Services;

public sealed class ThemeService
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private ResourceDictionary? currentThemeDictionary;

    public void ApplyTheme(ThemePreference preference)
    {
        var resolvedPreference = preference == ThemePreference.System
            ? ReadSystemThemePreference()
            : preference;

        var source = resolvedPreference == ThemePreference.Dark
            ? new Uri("Themes/DarkTheme.xaml", UriKind.Relative)
            : new Uri("Themes/LightTheme.xaml", UriKind.Relative);

        var resources = System.Windows.Application.Current.Resources.MergedDictionaries;
        if (currentThemeDictionary is not null)
        {
            resources.Remove(currentThemeDictionary);
        }

        currentThemeDictionary = new ResourceDictionary { Source = source };
        resources.Add(currentThemeDictionary);
    }

    private static ThemePreference ReadSystemThemePreference()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath, writable: false);
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int intValue && intValue == 0
                ? ThemePreference.Dark
                : ThemePreference.Light;
        }
        catch
        {
            return ThemePreference.Light;
        }
    }
}
