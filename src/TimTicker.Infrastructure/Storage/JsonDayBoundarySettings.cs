using System.Text.Json;
using TimTicker.Application.Abstractions;
using TimTicker.Application.Settings;

namespace TimTicker.Infrastructure.Storage;

public sealed class JsonDayBoundarySettings : IDayBoundarySettings
{
    private const string SettingsFileName = "settings.json";
    private readonly object _syncRoot = new();
    private readonly string _settingsPath;
    private DayEndTime _currentDayEndTime;

    public JsonDayBoundarySettings(IAppPaths appPaths)
        : this(appPaths?.DataDirectory ?? throw new ArgumentNullException(nameof(appPaths)))
    {
    }

    public JsonDayBoundarySettings(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _settingsPath = Path.Combine(Path.GetFullPath(dataDirectory), SettingsFileName);
        _currentDayEndTime = Load();
    }

    public DayEndTime CurrentDayEndTime
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentDayEndTime;
            }
        }
    }

    public void SetDayEndTime(DayEndTime value)
    {
        if (!DayBoundary.IsSupported(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        lock (_syncRoot)
        {
            if (_currentDayEndTime == value)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            string json = JsonSerializer.Serialize(new PersistedSettings((int)value));
            File.WriteAllText(_settingsPath, json);
            _currentDayEndTime = value;
        }
    }

    private DayEndTime Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return DayBoundary.DefaultDayEndTime;
        }

        try
        {
            PersistedSettings? settings = JsonSerializer.Deserialize<PersistedSettings>(File.ReadAllText(_settingsPath));
            DayEndTime value = (DayEndTime)(settings?.DayEndHour ?? (int)DayBoundary.DefaultDayEndTime);
            return DayBoundary.IsSupported(value) ? value : DayBoundary.DefaultDayEndTime;
        }
        catch (JsonException)
        {
            return DayBoundary.DefaultDayEndTime;
        }
        catch (IOException)
        {
            return DayBoundary.DefaultDayEndTime;
        }
        catch (UnauthorizedAccessException)
        {
            return DayBoundary.DefaultDayEndTime;
        }
    }

    private sealed record PersistedSettings(int DayEndHour);
}
