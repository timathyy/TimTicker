using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TimTicker.Infrastructure.Logging;

[ProviderAlias("RollingFile")]
public sealed class RollingFileLoggerProvider : ILoggerProvider
{
    private const string LogFilePrefix = "timticker-";
    private readonly string _logsDirectory;
    private readonly RollingFileLoggerOptions _options;
    private readonly object _writeLock = new();
    private DateOnly _activeDate;
    private string? _activeFilePath;
    private int _rollSequence;
    private bool _disposed;

    public RollingFileLoggerProvider(string logsDirectory, RollingFileLoggerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logsDirectory);

        _options = options ?? new RollingFileLoggerOptions();
        if (_options.MaxFileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                _options.MaxFileSizeBytes,
                "The maximum log file size must be greater than zero.");
        }

        if (_options.RetainedFileCountLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                _options.RetainedFileCountLimit,
                "The retained log file count must be greater than zero.");
        }

        _logsDirectory = Path.GetFullPath(logsDirectory);
        Directory.CreateDirectory(_logsDirectory);
        DeleteExpiredFiles(activeFilePath: null);
    }

    public ILogger CreateLogger(string categoryName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new RollingFileLogger(categoryName, this);
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private static Dictionary<string, string?>? GetProperties<TState>(TState state)
    {
        if (state is not IEnumerable<KeyValuePair<string, object?>> structuredState)
        {
            return null;
        }

        Dictionary<string, string?> properties = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> property in structuredState)
        {
            if (string.Equals(property.Key, "{OriginalFormat}", StringComparison.Ordinal))
            {
                continue;
            }

            properties[property.Key] = Convert.ToString(property.Value, CultureInfo.InvariantCulture);
        }

        return properties.Count == 0 ? null : properties;
    }

    private void Write<TState>(
        string categoryName,
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (_disposed)
        {
            return;
        }

        DateTimeOffset timestamp = DateTimeOffset.Now;
        var entry = new
        {
            Timestamp = timestamp,
            Level = logLevel.ToString(),
            Category = categoryName,
            EventId = eventId.Id,
            EventName = eventId.Name,
            Message = formatter(state, exception),
            Properties = GetProperties(state),
            ExceptionType = exception?.GetType().FullName,
            ExceptionCode = exception?.HResult,
            StackTrace = exception?.StackTrace,
        };

        string line = JsonSerializer.Serialize(entry) + Environment.NewLine;
        int lineSize = Encoding.UTF8.GetByteCount(line);

        lock (_writeLock)
        {
            try
            {
                string activeFilePath = GetActiveFilePath(timestamp, lineSize);
                File.AppendAllText(activeFilePath, line, Encoding.UTF8);
                DeleteExpiredFiles(activeFilePath);
            }
            catch (IOException)
            {
                // Logging must never terminate the application.
            }
            catch (UnauthorizedAccessException)
            {
                // Logging must never terminate the application.
            }
        }
    }

    private string GetActiveFilePath(DateTimeOffset timestamp, int pendingBytes)
    {
        DateOnly currentDate = DateOnly.FromDateTime(timestamp.LocalDateTime);
        if (_activeFilePath is null || currentDate != _activeDate)
        {
            _activeDate = currentDate;
            _activeFilePath = Path.Combine(
                _logsDirectory,
                $"{LogFilePrefix}{currentDate:yyyyMMdd}.log");
            _rollSequence = 0;
        }

        FileInfo activeFile = new(_activeFilePath);
        if (!activeFile.Exists || activeFile.Length + pendingBytes <= _options.MaxFileSizeBytes)
        {
            return _activeFilePath;
        }

        do
        {
            _rollSequence++;
            _activeFilePath = Path.Combine(
                _logsDirectory,
                $"{LogFilePrefix}{currentDate:yyyyMMdd}-{timestamp:HHmmssfff}-{_rollSequence:000}.log");
        }
        while (File.Exists(_activeFilePath));

        return _activeFilePath;
    }

    private void DeleteExpiredFiles(string? activeFilePath)
    {
        FileInfo[] logFiles = new DirectoryInfo(_logsDirectory)
            .EnumerateFiles($"{LogFilePrefix}*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenByDescending(file => file.Name, StringComparer.Ordinal)
            .ToArray();

        IEnumerable<FileInfo> filesToDelete = logFiles
            .Where(file => !string.Equals(file.FullName, activeFilePath, StringComparison.OrdinalIgnoreCase))
            .Skip(Math.Max(0, _options.RetainedFileCountLimit - (activeFilePath is null ? 0 : 1)));

        foreach (FileInfo file in filesToDelete)
        {
            try
            {
                file.Delete();
            }
            catch (IOException)
            {
                // A locked historical log can be retried during the next write.
            }
            catch (UnauthorizedAccessException)
            {
                // A protected historical log can be retried during the next write.
            }
        }
    }

    private sealed class RollingFileLogger(string categoryName, RollingFileLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            if (IsEnabled(logLevel))
            {
                provider.Write(categoryName, logLevel, eventId, state, exception, formatter);
            }
        }
    }
}
