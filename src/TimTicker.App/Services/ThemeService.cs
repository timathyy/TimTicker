using Microsoft.Win32;
using System.Windows;
using System.Windows.Threading;

namespace TimTicker.App.Services;

public sealed class ThemeService : IThemeService, IDisposable
{
    private const string ThemePathFragment = "Themes/";
    private const string PersonalizeRegistryPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public ThemeService()
    {
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public AppTheme CurrentTheme { get; private set; } = AppTheme.System;

    public AppTheme EffectiveTheme { get; private set; } = AppTheme.Light;

    public event EventHandler? ThemeChanged;

    public void Apply(AppTheme theme)
    {
        CurrentTheme = theme;
        ApplyEffectiveTheme(resolveSystemTheme: true);
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void ApplyEffectiveTheme(bool resolveSystemTheme)
    {
        AppTheme effectiveTheme = CurrentTheme == AppTheme.System
            ? GetSystemTheme()
            : CurrentTheme;

        if (!resolveSystemTheme && effectiveTheme == EffectiveTheme)
        {
            return;
        }

        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        ResourceDictionary? currentTheme = dictionaries.FirstOrDefault(IsThemeDictionary);

        ResourceDictionary nextTheme = new()
        {
            Source = new Uri(
                $"pack://application:,,,/TimTicker.App;component/Themes/{effectiveTheme}.xaml",
                UriKind.Absolute),
        };

        if (currentTheme is null)
        {
            dictionaries.Insert(0, nextTheme);
        }
        else
        {
            int index = dictionaries.IndexOf(currentTheme);
            dictionaries[index] = nextTheme;
        }

        EffectiveTheme = effectiveTheme;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static AppTheme GetSystemTheme()
    {
        object? registryValue = Registry.GetValue(PersonalizeRegistryPath, "AppsUseLightTheme", 1);
        return registryValue is int value && value == 0
            ? AppTheme.Dark
            : AppTheme.Light;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (CurrentTheme != AppTheme.System)
        {
            return;
        }

        Dispatcher dispatcher = System.Windows.Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            ApplyEffectiveTheme(resolveSystemTheme: false);
        }
        else
        {
            _ = dispatcher.BeginInvoke(() => ApplyEffectiveTheme(resolveSystemTheme: false));
        }
    }

    private static bool IsThemeDictionary(ResourceDictionary dictionary)
    {
        string source = dictionary.Source?.OriginalString ?? string.Empty;
        return source.Contains(ThemePathFragment, StringComparison.OrdinalIgnoreCase)
            && (source.EndsWith("Light.xaml", StringComparison.OrdinalIgnoreCase)
                || source.EndsWith("Dark.xaml", StringComparison.OrdinalIgnoreCase));
    }
}
