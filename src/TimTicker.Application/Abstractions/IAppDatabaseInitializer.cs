namespace TimTicker.Application.Abstractions;

public interface IAppDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
