using TimTicker.App.ViewModels;

namespace TimTicker.App.Services;

public interface INavigationService
{
    ViewModelBase Navigate(AppPage page);
}
