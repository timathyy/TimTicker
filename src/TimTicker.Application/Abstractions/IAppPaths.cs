namespace TimTicker.Application.Abstractions;

public interface IAppPaths
{
    string RootDirectory { get; }

    string DataDirectory { get; }

    string DatabasePath { get; }

    string LogsDirectory { get; }

    void EnsureDirectoriesExist();
}
