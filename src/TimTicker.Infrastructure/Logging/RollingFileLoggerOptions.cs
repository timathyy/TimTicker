namespace TimTicker.Infrastructure.Logging;

public sealed class RollingFileLoggerOptions
{
    public const long DefaultMaxFileSizeBytes = 5 * 1024 * 1024;
    public const int DefaultRetainedFileCountLimit = 14;

    public long MaxFileSizeBytes { get; init; } = DefaultMaxFileSizeBytes;

    public int RetainedFileCountLimit { get; init; } = DefaultRetainedFileCountLimit;
}
