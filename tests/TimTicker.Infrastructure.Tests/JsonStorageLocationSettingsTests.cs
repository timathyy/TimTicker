using TimTicker.Infrastructure.Storage;

namespace TimTicker.Infrastructure.Tests;

public sealed class JsonStorageLocationSettingsTests
{
    [Fact]
    public void DefaultsToConfiguredRootAndPersistsSelectedLocation()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "TimTicker.Tests", Guid.NewGuid().ToString("N"));
        string configurationDirectory = Path.Combine(testRoot, "Configuration");
        string defaultDirectory = Path.Combine(testRoot, "Default");
        string selectedDirectory = Path.Combine(testRoot, "Selected");

        try
        {
            JsonStorageLocationSettings first = new(configurationDirectory, defaultDirectory);
            Assert.Equal(Path.GetFullPath(defaultDirectory), first.RootDirectory);

            first.SetRootDirectory(selectedDirectory);
            JsonStorageLocationSettings reloaded = new(configurationDirectory, defaultDirectory);

            Assert.Equal(Path.GetFullPath(selectedDirectory), reloaded.RootDirectory);
            Assert.True(Directory.Exists(selectedDirectory));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void InvalidSettingsFallBackToDefaultRoot()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "TimTicker.Tests", Guid.NewGuid().ToString("N"));
        string configurationDirectory = Path.Combine(testRoot, "Configuration");
        string defaultDirectory = Path.Combine(testRoot, "Default");

        try
        {
            Directory.CreateDirectory(configurationDirectory);
            File.WriteAllText(Path.Combine(configurationDirectory, "storage-location.json"), "not json");

            JsonStorageLocationSettings settings = new(configurationDirectory, defaultDirectory);

            Assert.Equal(Path.GetFullPath(defaultDirectory), settings.RootDirectory);
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
