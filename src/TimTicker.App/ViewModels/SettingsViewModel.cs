using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimTicker.App.Services;

namespace TimTicker.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly IThemeService _themeService;

    [ObservableProperty]
    private string _currentThemeName;

    public SettingsViewModel(IThemeService themeService)
    {
        _themeService = themeService;
        _currentThemeName = GetThemeName();
        _themeService.ThemeChanged += OnThemeChanged;
    }

    public void Dispose()
    {
        _themeService.ThemeChanged -= OnThemeChanged;
    }

    [RelayCommand]
    private void UseSystemTheme()
    {
        _themeService.Apply(AppTheme.System);
    }

    [RelayCommand]
    private void UseLightTheme()
    {
        _themeService.Apply(AppTheme.Light);
    }

    [RelayCommand]
    private void UseDarkTheme()
    {
        _themeService.Apply(AppTheme.Dark);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        CurrentThemeName = GetThemeName();
    }

    private string GetThemeName()
    {
        return _themeService.CurrentTheme switch
        {
            AppTheme.System => $"跟随系统（当前为{GetEffectiveThemeName()}）",
            AppTheme.Light => "浅色",
            AppTheme.Dark => "深色",
            _ => throw new InvalidOperationException("Unknown application theme."),
        };
    }

    private string GetEffectiveThemeName()
    {
        return _themeService.EffectiveTheme == AppTheme.Dark ? "深色" : "浅色";
    }
}
