using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimTicker.App.Services;
using TimTicker.Application.Abstractions;
using TimTicker.Application.Settings;

namespace TimTicker.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly IThemeService _themeService;
    private readonly IDayBoundarySettings _dayBoundarySettings;
    private readonly IStorageLocationSettings _storageLocationSettings;
    private readonly IFolderPickerService _folderPickerService;
    private readonly string _activeStoragePath;
    private readonly string _applicationVersionText;

    [ObservableProperty]
    private string _currentThemeName;

    [ObservableProperty]
    private string _currentDayEndTimeName;

    [ObservableProperty]
    private string _localStoragePath;

    [ObservableProperty]
    private string _storageLocationHint;

    public SettingsViewModel(
        IThemeService themeService,
        IDayBoundarySettings dayBoundarySettings,
        IStorageLocationSettings storageLocationSettings,
        IFolderPickerService folderPickerService,
        IAppPaths appPaths)
    {
        _themeService = themeService;
        _dayBoundarySettings = dayBoundarySettings;
        _storageLocationSettings = storageLocationSettings;
        _folderPickerService = folderPickerService;
        _activeStoragePath = appPaths.RootDirectory;
        _applicationVersionText = GetApplicationVersionText();
        _currentThemeName = GetThemeName();
        _currentDayEndTimeName = DayBoundary.ToDisplayText(dayBoundarySettings.CurrentDayEndTime);
        _localStoragePath = storageLocationSettings.RootDirectory;
        _storageLocationHint = GetStorageLocationHint();
        _themeService.ThemeChanged += OnThemeChanged;
    }

    public void Dispose()
    {
        _themeService.ThemeChanged -= OnThemeChanged;
    }

    public AppTheme SelectedTheme => _themeService.CurrentTheme;

    public DayEndTime SelectedDayEndTime => _dayBoundarySettings.CurrentDayEndTime;

    public string ApplicationVersionText => _applicationVersionText;

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

    [RelayCommand]
    private void BrowseStorageLocation()
    {
        string? selectedDirectory = _folderPickerService.PickFolder(LocalStoragePath);
        if (selectedDirectory is null)
        {
            return;
        }

        try
        {
            _storageLocationSettings.SetRootDirectory(selectedDirectory);
            LocalStoragePath = _storageLocationSettings.RootDirectory;
            StorageLocationHint = GetStorageLocationHint();
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            StorageLocationHint = "无法保存该位置，请确认文件夹可写后重试。";
        }
    }

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

    private string GetStorageLocationHint()
    {
        return StringComparer.OrdinalIgnoreCase.Equals(_activeStoragePath, _storageLocationSettings.RootDirectory)
            ? "当前数据库、设置和日志保存在此文件夹。"
            : "新位置将在重启后生效；现有数据不会自动移动。";
    }

    private static string GetApplicationVersionText()
    {
        Version? version = typeof(App).Assembly.GetName().Version;
        return version is null
            ? "TimTicker · 本地优先的日历与时间记录工具"
            : $"TimTicker v{version.Major}.{version.Minor}.{version.Build} · 本地优先的日历与时间记录工具";
    }
}
