using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimTicker.Infrastructure.Logging;

namespace TimTicker.Infrastructure.Tests;

public sealed class RollingFileLoggerProviderTests
{
    private static readonly Action<ILogger, int, Exception?> LogStartupFailed =
        LoggerMessage.Define<int>(
            LogLevel.Error,
            new EventId(10, nameof(LogStartupFailed)),
            "Startup failed with code {Code}");

    private static readonly Action<ILogger, int, string, Exception?> LogTestEntry =
        LoggerMessage.Define<int, string>(
            LogLevel.Information,
            new EventId(11, nameof(LogTestEntry)),
            "Log entry {Index}: {Padding}");

    [Fact]
    public void LoggerWritesStructuredJsonWithoutExceptionMessage()
    {
        string testRoot = CreateTestRoot();

        try
        {
            using RollingFileLoggerProvider provider = new(testRoot);
            ILogger logger = provider.CreateLogger("TimTicker.Tests");

            LogStartupFailed(logger, 42, new InvalidOperationException("private task text"));

            string logFile = Assert.Single(Directory.GetFiles(testRoot, "timticker-*.log"));
            string logEntry = File.ReadAllText(logFile);
            using JsonDocument document = JsonDocument.Parse(logEntry);
            JsonElement root = document.RootElement;

            Assert.Equal("Error", root.GetProperty("Level").GetString());
            Assert.Equal("TimTicker.Tests", root.GetProperty("Category").GetString());
            Assert.Equal("Startup failed with code 42", root.GetProperty("Message").GetString());
            Assert.Equal("42", root.GetProperty("Properties").GetProperty("Code").GetString());
            Assert.Equal(
                typeof(InvalidOperationException).FullName,
                root.GetProperty("ExceptionType").GetString());
            Assert.DoesNotContain("private task text", logEntry, StringComparison.Ordinal);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void LoggerRollsBySizeAndRetainsConfiguredFileCount()
    {
        string testRoot = CreateTestRoot();

        try
        {
            RollingFileLoggerOptions options = new()
            {
                MaxFileSizeBytes = 300,
                RetainedFileCountLimit = 2,
            };
            using RollingFileLoggerProvider provider = new(testRoot, options);
            ILogger logger = provider.CreateLogger("TimTicker.Tests");

            for (int index = 0; index < 8; index++)
            {
                LogTestEntry(logger, index, new string('x', 180), null);
            }

            Assert.Equal(2, Directory.GetFiles(testRoot, "timticker-*.log").Length);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    private static string CreateTestRoot()
    {
        return Path.Combine(Path.GetTempPath(), "TimTicker.Tests", Guid.NewGuid().ToString("N"));
    }

    private static void DeleteTestRoot(string testRoot)
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }
}
