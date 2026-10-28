using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimTicker.Application.Abstractions;

namespace TimTicker.Infrastructure.Persistence;

public sealed partial class AppDatabaseInitializer(
    IDbContextFactory<TimTickerDbContext> contextFactory,
    ILogger<AppDatabaseInitializer> logger) : IAppDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using TimTickerDbContext context =
            await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        LogDatabaseInitialized(logger);
    }

    [LoggerMessage(
        EventId = 100,
        Level = LogLevel.Information,
        Message = "TimTicker database migrations applied successfully")]
    private static partial void LogDatabaseInitialized(ILogger logger);
}
