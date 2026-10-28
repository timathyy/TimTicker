using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimTicker.Application.Abstractions;
using TimTicker.App.Services;
using TimTicker.App.ViewModels;
using TimTicker.Infrastructure;
using TimTicker.Infrastructure.Logging;
using TimTicker.Infrastructure.Storage;

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
        await _host.Services
            .GetRequiredService<IAppDatabaseInitializer>()
            .InitializeAsync();

        ILogger<App> logger = _host.Services.GetRequiredService<ILogger<App>>();
        LogApplicationStarted(logger);

        _host.Services.GetRequiredService<IThemeService>().Apply(AppTheme.System);

        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            ILogger<App>? logger = _host.Services.GetService<ILogger<App>>();
            if (logger is not null)
            {
                LogApplicationStopping(logger);
            }

            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static IHost CreateHost()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                ContentRootPath = AppContext.BaseDirectory,
            });
        AppPaths appPaths = new();
        appPaths.EnsureDirectoriesExist();
        RollingFileLoggerOptions fileLoggerOptions = CreateFileLoggerOptions(builder.Configuration);

        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();
        builder.Logging.AddProvider(new RollingFileLoggerProvider(appPaths.LogsDirectory, fileLoggerOptions));

        builder.Services.AddSingleton<IAppPaths>(appPaths);
        builder.Services.AddTimTickerInfrastructure(appPaths.DatabasePath);
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

    private static RollingFileLoggerOptions CreateFileLoggerOptions(ConfigurationManager configuration)
    {
        long maxFileSizeBytes = RollingFileLoggerOptions.DefaultMaxFileSizeBytes;
        if (long.TryParse(
            configuration["FileLogging:MaxFileSizeBytes"],
            out long configuredMaxFileSizeBytes))
        {
            maxFileSizeBytes = configuredMaxFileSizeBytes;
        }

        int retainedFileCountLimit = RollingFileLoggerOptions.DefaultRetainedFileCountLimit;
        if (int.TryParse(
            configuration["FileLogging:RetainedFileCountLimit"],
            out int configuredRetainedFileCountLimit))
        {
            retainedFileCountLimit = configuredRetainedFileCountLimit;
        }

        return new RollingFileLoggerOptions
        {
            MaxFileSizeBytes = maxFileSizeBytes,
            RetainedFileCountLimit = retainedFileCountLimit,
        };
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

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "TimTicker application started")]
    private static partial void LogApplicationStarted(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "TimTicker application stopping")]
    private static partial void LogApplicationStopping(ILogger logger);
}

