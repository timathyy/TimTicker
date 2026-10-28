using TimTicker.Infrastructure.Storage;

namespace TimTicker.Infrastructure.Tests;

public sealed class AppPathsTests
{
    [Fact]
    public void EnsureDirectoriesExistCreatesStableApplicationLayout()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "TimTicker.Tests", Guid.NewGuid().ToString("N"));

        try
        {
            AppPaths paths = new(testRoot);

            paths.EnsureDirectoriesExist();

            Assert.Equal(Path.GetFullPath(testRoot), paths.RootDirectory);
            Assert.Equal(Path.Combine(testRoot, "Data"), paths.DataDirectory);
            Assert.Equal(Path.Combine(testRoot, "Data", "timticker.db"), paths.DatabasePath);
            Assert.Equal(Path.Combine(testRoot, "Logs"), paths.LogsDirectory);
            Assert.True(Directory.Exists(paths.DataDirectory));
            Assert.True(Directory.Exists(paths.LogsDirectory));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }
}
