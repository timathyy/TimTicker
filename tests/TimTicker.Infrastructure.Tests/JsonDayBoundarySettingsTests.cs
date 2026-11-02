using TimTicker.Application.Settings;
using TimTicker.Infrastructure.Storage;

namespace TimTicker.Infrastructure.Tests;

public sealed class JsonDayBoundarySettingsTests
{
    [Fact]
    public void DefaultsToSixAmAndPersistsSelectedValue()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "TimTicker.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            AppPaths paths = new(testRoot);
            paths.EnsureDirectoriesExist();
            JsonDayBoundarySettings first = new(paths);

            Assert.Equal(DayEndTime.SixAm, first.CurrentDayEndTime);

            first.SetDayEndTime(DayEndTime.TwoAm);
            JsonDayBoundarySettings reloaded = new(paths);

            Assert.Equal(DayEndTime.TwoAm, reloaded.CurrentDayEndTime);
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
