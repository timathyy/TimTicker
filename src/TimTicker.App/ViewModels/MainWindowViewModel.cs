using CommunityToolkit.Mvvm.ComponentModel;
using TimTicker.App.Services;

namespace TimTicker.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private NavigationItemViewModel? _selectedNavigationItem;

    public MainWindowViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        NavigationItems =
        [
            new(AppPage.Today, "今天", "\uE80F"),
            new(AppPage.Tasks, "任务", "\uE9D5"),
            new(AppPage.Calendar, "日历", "\uE787"),
            new(AppPage.Settings, "设置", "\uE713"),
        ];

        _selectedNavigationItem = NavigationItems[0];
        _currentPage = navigationService.Navigate(AppPage.Today);
    }

    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }

    partial void OnSelectedNavigationItemChanged(NavigationItemViewModel? value)
    {
        if (value is not null)
        {
            CurrentPage = _navigationService.Navigate(value.Page);
        }
    }
}
