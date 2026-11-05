namespace TimTicker.Application.Settings;

public interface IStorageLocationSettings
{
    string RootDirectory { get; }

    void SetRootDirectory(string rootDirectory);
}
