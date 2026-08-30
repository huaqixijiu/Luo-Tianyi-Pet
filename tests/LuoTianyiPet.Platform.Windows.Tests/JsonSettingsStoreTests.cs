using LuoTianyiPet.Core;

namespace LuoTianyiPet.Platform.Windows.Tests;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsWindowPreferences()
    {
        string testDirectory = CreateTestDirectory();
        try
        {
            JsonSettingsStore store = new(new LocalAppPaths(testDirectory));
            AppSettings expected = new()
            {
                Media = new MediaPreferences
                {
                    EnableCloudMusicDetection = false,
                    TargetProcessName = "custom-player.exe",
                },
                Window = new WindowPreferences
                {
                    AlwaysOnTop = true,
                    Left = 123.5,
                    Top = 456.25,
                },
            };

            await store.SaveAsync(expected);
            AppSettings actual = await store.LoadAsync();

            Assert.Equal(expected, actual);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Load_LegacySettingsWithoutMedia_UsesSafeMediaDefaults()
    {
        string testDirectory = CreateTestDirectory();
        try
        {
            LocalAppPaths paths = new(testDirectory);
            Directory.CreateDirectory(testDirectory);
            await File.WriteAllTextAsync(
                paths.SettingsFile,
                """
                {
                  "schemaVersion": 1,
                  "window": {
                    "alwaysOnTop": true
                  }
                }
                """);
            JsonSettingsStore store = new(paths);

            AppSettings actual = await store.LoadAsync();

            Assert.True(actual.Window.AlwaysOnTop);
            Assert.Equal(new MediaPreferences(), actual.Media);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Load_WithInvalidJson_ReturnsDefaultsAndPreservesInput()
    {
        string testDirectory = CreateTestDirectory();
        try
        {
            LocalAppPaths paths = new(testDirectory);
            Directory.CreateDirectory(testDirectory);
            await File.WriteAllTextAsync(paths.SettingsFile, "{not-json");
            JsonSettingsStore store = new(paths);

            AppSettings actual = await store.LoadAsync();

            Assert.Equal(new AppSettings(), actual);
            Assert.False(File.Exists(paths.SettingsFile));
            Assert.Single(Directory.GetFiles(testDirectory, "settings.corrupt-*.json"));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void SaveAsync_CanBeSynchronouslyWaitedDuringWindowClose()
    {
        string testDirectory = CreateTestDirectory();
        using ManualResetEventSlim completed = new();
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());
                JsonSettingsStore store = new(new LocalAppPaths(testDirectory));
                store.SaveAsync(new AppSettings()).GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                completed.Set();
            }
        })
        {
            IsBackground = true,
        };

        thread.Start();
        Assert.True(completed.Wait(TimeSpan.FromSeconds(5)), "Settings save deadlocked during synchronous window close.");
        Assert.Null(failure);
        Directory.Delete(testDirectory, recursive: true);
    }

    private static string CreateTestDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "LuoTianyiPet.Tests", Guid.NewGuid().ToString("N"));
    }

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state)
        {
            // Intentionally do not pump callbacks, matching a synchronously blocked UI thread.
        }
    }
}
