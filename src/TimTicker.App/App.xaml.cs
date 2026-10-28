using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimTicker.App.Services;
using TimTicker.App.ViewModels;

namespace TimTicker.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = CreateHost();
        RegisterExceptionHandlers();
        await _host.StartAsync();

        _host.Services.GetRequiredService<IThemeService>().Apply(AppTheme.System);

        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static IHost CreateHost()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();

        builder.Services.AddSingleton<IThemeService, ThemeService>();
        builder.Services.AddSingleton<INavigationService, NavigationService>();

        builder.Services.AddSingleton<TodayViewModel>();
        builder.Services.AddSingleton<TasksViewModel>();
        builder.Services.AddSingleton<CalendarViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<MainWindowViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        return builder.Build();
    }

    private void RegisterExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCriticalException(e.Exception, "Unhandled UI exception");
        MessageBox.Show(
            "TimTicker 遇到了意外问题。请重新启动应用。",
            "TimTicker",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            LogCriticalException(exception, "Unhandled application exception");
        }
    }

    private void LogCriticalException(Exception exception, string message)
    {
        ILogger<App>? logger = _host?.Services.GetService<ILogger<App>>();
        if (logger is not null)
        {
            LogCritical(logger, exception, message);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Critical, Message = "{Message}")]
    private static partial void LogCritical(ILogger logger, Exception exception, string message);
}

