using System.Text.Json;
using TimTicker.Application.Settings;

namespace TimTicker.Infrastructure.Storage;

public sealed class JsonStorageLocationSettings : IStorageLocationSettings
{
    private const string SettingsFileName = "storage-location.json";
    private readonly object _syncRoot = new();
    private readonly string _defaultRootDirectory;
    private readonly string _settingsPath;
    private string _rootDirectory;

    public JsonStorageLocationSettings()
        : this(AppPaths.GetDefaultRootDirectory(), AppPaths.GetDefaultRootDirectory())
    {
    }

    public JsonStorageLocationSettings(string configurationDirectory, string defaultRootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultRootDirectory);

        _settingsPath = Path.Combine(Path.GetFullPath(configurationDirectory), SettingsFileName);
        _defaultRootDirectory = Path.GetFullPath(defaultRootDirectory);
        _rootDirectory = Load();
    }

    public string RootDirectory
    {
        get
        {
            lock (_syncRoot)
            {
                return _rootDirectory;
            }
        }
    }

    public void SetRootDirectory(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        string fullRootDirectory = Path.GetFullPath(rootDirectory);
        lock (_syncRoot)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(fullRootDirectory, _rootDirectory))
            {
                return;
            }

            Directory.CreateDirectory(fullRootDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            string json = JsonSerializer.Serialize(new PersistedSettings(fullRootDirectory));
            File.WriteAllText(_settingsPath, json);
            _rootDirectory = fullRootDirectory;
        }
    }

    private string Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return _defaultRootDirectory;
        }

        try
        {
            PersistedSettings? settings = JsonSerializer.Deserialize<PersistedSettings>(File.ReadAllText(_settingsPath));
            return string.IsNullOrWhiteSpace(settings?.RootDirectory)
                ? _defaultRootDirectory
                : Path.GetFullPath(settings.RootDirectory);
        }
        catch (JsonException)
        {
            return _defaultRootDirectory;
        }
        catch (IOException)
        {
            return _defaultRootDirectory;
        }
        catch (UnauthorizedAccessException)
        {
            return _defaultRootDirectory;
        }
        catch (ArgumentException)
        {
            return _defaultRootDirectory;
        }
        catch (NotSupportedException)
        {
            return _defaultRootDirectory;
        }
    }

    private sealed record PersistedSettings(string RootDirectory);
}
