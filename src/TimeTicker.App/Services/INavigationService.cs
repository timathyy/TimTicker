using TimeTicker.App.ViewModels;

namespace TimeTicker.App.Services;

public interface INavigationService
{
    ViewModelBase Navigate(AppPage page);
}
