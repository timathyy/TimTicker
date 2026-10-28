using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimTicker.App.Services;

namespace TimTicker.App.ViewModels;

public sealed partial class SettingsViewModel(IThemeService themeService) : ViewModelBase
{
    [ObservableProperty]
    private string _currentThemeName = "浅色";

    [RelayCommand]
    private void UseLightTheme()
    {
        themeService.Apply(AppTheme.Light);
        CurrentThemeName = "浅色";
    }

    [RelayCommand]
    private void UseDarkTheme()
    {
        themeService.Apply(AppTheme.Dark);
        CurrentThemeName = "深色";
    }
}
