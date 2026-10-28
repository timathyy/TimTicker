namespace TimTicker.App.Services;

public interface IThemeService
{
    AppTheme CurrentTheme { get; }

    AppTheme EffectiveTheme { get; }

    event EventHandler? ThemeChanged;

    void Apply(AppTheme theme);
}
