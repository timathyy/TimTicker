using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimTicker.App.Services;
using TimTicker.Application.Settings;

namespace TimTicker.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly IThemeService _themeService;
    private readonly IDayBoundarySettings _dayBoundarySettings;

    [ObservableProperty]
    private string _currentThemeName;

    [ObservableProperty]
    private string _currentDayEndTimeName;

    public SettingsViewModel(
        IThemeService themeService,
        IDayBoundarySettings dayBoundarySettings)
    {
        _themeService = themeService;
        _dayBoundarySettings = dayBoundarySettings;
        _currentThemeName = GetThemeName();
        _currentDayEndTimeName = DayBoundary.ToDisplayText(dayBoundarySettings.CurrentDayEndTime);
        _themeService.ThemeChanged += OnThemeChanged;
    }

    public void Dispose()
    {
        _themeService.ThemeChanged -= OnThemeChanged;
    }

    public AppTheme SelectedTheme => _themeService.CurrentTheme;

    public DayEndTime SelectedDayEndTime => _dayBoundarySettings.CurrentDayEndTime;

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

    [RelayCommand]
    private void UseMidnightDayEnd() => SetDayEndTime(DayEndTime.Midnight);

    [RelayCommand]
    private void UseTwoAmDayEnd() => SetDayEndTime(DayEndTime.TwoAm);

    [RelayCommand]
    private void UseFourAmDayEnd() => SetDayEndTime(DayEndTime.FourAm);

    [RelayCommand]
    private void UseSixAmDayEnd() => SetDayEndTime(DayEndTime.SixAm);

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        CurrentThemeName = GetThemeName();
        OnPropertyChanged(nameof(SelectedTheme));
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

    private void SetDayEndTime(DayEndTime value)
    {
        _dayBoundarySettings.SetDayEndTime(value);
        CurrentDayEndTimeName = DayBoundary.ToDisplayText(value);
        OnPropertyChanged(nameof(SelectedDayEndTime));
    }
}
