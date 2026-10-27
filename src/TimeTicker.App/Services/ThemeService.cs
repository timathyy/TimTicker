using System.Windows;

namespace TimeTicker.App.Services;

public sealed class ThemeService : IThemeService
{
    private const string ThemePathFragment = "/Themes/";

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public void Apply(AppTheme theme)
    {
        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        ResourceDictionary? currentTheme = dictionaries.FirstOrDefault(IsThemeDictionary);

        ResourceDictionary nextTheme = new()
        {
            Source = new Uri($"/TimeTicker.App;component/Themes/{theme}.xaml", UriKind.Relative),
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

        CurrentTheme = theme;
    }

    private static bool IsThemeDictionary(ResourceDictionary dictionary)
    {
        string source = dictionary.Source?.OriginalString ?? string.Empty;
        return source.Contains(ThemePathFragment, StringComparison.OrdinalIgnoreCase)
            && (source.EndsWith("Light.xaml", StringComparison.OrdinalIgnoreCase)
                || source.EndsWith("Dark.xaml", StringComparison.OrdinalIgnoreCase));
    }
}
