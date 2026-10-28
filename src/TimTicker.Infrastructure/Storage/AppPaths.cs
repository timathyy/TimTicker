using TimTicker.Application.Abstractions;

namespace TimTicker.Infrastructure.Storage;

public sealed class AppPaths : IAppPaths
{
    private const string ProductDirectoryName = "TimTicker";
    private const string DatabaseFileName = "timticker.db";

    public AppPaths()
        : this(GetDefaultRootDirectory())
    {
    }

    public AppPaths(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        RootDirectory = Path.GetFullPath(rootDirectory);
        DataDirectory = Path.Combine(RootDirectory, "Data");
        DatabasePath = Path.Combine(DataDirectory, DatabaseFileName);
        LogsDirectory = Path.Combine(RootDirectory, "Logs");
    }

    public string RootDirectory { get; }

    public string DataDirectory { get; }

    public string DatabasePath { get; }

    public string LogsDirectory { get; }

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }

    private static string GetDefaultRootDirectory()
    {
        string localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);

        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException("The local application data directory is unavailable.");
        }

        return Path.Combine(localApplicationData, ProductDirectoryName);
    }
}
