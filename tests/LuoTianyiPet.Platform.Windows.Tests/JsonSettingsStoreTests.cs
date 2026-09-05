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
                    MusicAnimationSelection = PetVisualState.MusicSwayAnimation,
                },
                Volume = new VolumePreferences
                {
                    EnableMouseWheelControl = false,
                    EnableExternalChangeFeedback = false,
                    MouseWheelStepPercent = 5,
                },
                Genshin = new GenshinPreferences
                {
                    EnableIntegration = false,
                    ProcessNames = "CustomGame.exe",
                    StatusPollIntervalMilliseconds = 3000,
                },
                Notifications = new MessageNotificationPreferences
                {
                    EnableMessageReminders = false,
                    DuplicateWindowMilliseconds = 4500,
                    QqProcessNames = "CustomQQ.exe",
                    WeChatProcessNames = "CustomWeChat.exe",
                },
                Appearance = new AppearancePreferences
                {
                    FullBodyStyle = AppearanceOptionIds.FullBodyClassicCatEars,
                    BunEatingStyle = AppearanceOptionIds.BunEatingNew,
                    DisplayScalePercent = 135,
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
            Assert.Equal(new VolumePreferences(), actual.Volume);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Load_Version5Settings_AddsNotificationDefaults()
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
                  "schemaVersion": 5,
                  "window": {
                    "alwaysOnTop": true,
                    "left": 24
                  }
                }
                """);
            JsonSettingsStore store = new(paths);

            AppSettings actual = await store.LoadAsync();

            Assert.Equal(AppSettings.CurrentSchemaVersion, actual.SchemaVersion);
            Assert.True(actual.Window.AlwaysOnTop);
            Assert.Equal(24, actual.Window.Left);
            Assert.Equal(new MessageNotificationPreferences(), actual.Notifications);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Load_Version6Settings_DoesNotProbeNotificationAccessBeforeConsent()
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
                  "schemaVersion": 6,
                  "notifications": {
                    "enableMessageReminders": true
                  }
                }
                """);
            JsonSettingsStore store = new(paths);

            AppSettings actual = await store.LoadAsync();

            Assert.Equal(AppSettings.CurrentSchemaVersion, actual.SchemaVersion);
            Assert.True(actual.Notifications.EnableMessageReminders);
            Assert.False(actual.Notifications.WindowsNotificationAccessGranted);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Load_Version4Settings_AddsGenshinDefaults()
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
                  "schemaVersion": 4,
                  "window": {
                    "alwaysOnTop": true,
                    "left": 42
                  }
                }
                """);
            JsonSettingsStore store = new(paths);

            AppSettings actual = await store.LoadAsync();

            Assert.Equal(AppSettings.CurrentSchemaVersion, actual.SchemaVersion);
            Assert.True(actual.Window.AlwaysOnTop);
            Assert.Equal(42, actual.Window.Left);
            Assert.Equal(new GenshinPreferences(), actual.Genshin);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Load_Version3Settings_AddsVolumeDefaults()
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
                  "schemaVersion": 3,
                  "window": {
                    "alwaysOnTop": true
                  },
                  "media": {
                    "silenceGraceMilliseconds": 1000
                  }
                }
                """);
            JsonSettingsStore store = new(paths);

            AppSettings actual = await store.LoadAsync();

            Assert.Equal(AppSettings.CurrentSchemaVersion, actual.SchemaVersion);
            Assert.True(actual.Window.AlwaysOnTop);
            Assert.Equal(new VolumePreferences(), actual.Volume);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData(1500, 1000)]
    [InlineData(800, 800)]
    public async Task Load_Version1Media_MigratesOnlyTheOldDefaultGracePeriod(
        int storedGraceMilliseconds,
        int expectedGraceMilliseconds)
    {
        string testDirectory = CreateTestDirectory();
        try
        {
            LocalAppPaths paths = new(testDirectory);
            Directory.CreateDirectory(testDirectory);
            await File.WriteAllTextAsync(
                paths.SettingsFile,
                $$"""
                {
                  "schemaVersion": 1,
                  "window": {
                    "alwaysOnTop": true,
                    "left": 123.5
                  },
                  "media": {
                    "enableCloudMusicDetection": false,
                    "targetProcessName": "custom-player.exe",
                    "pollIntervalMilliseconds": 400,
                    "silenceGraceMilliseconds": {{storedGraceMilliseconds}},
                    "audiblePeakThreshold": 0.002
                  }
                }
                """);
            JsonSettingsStore store = new(paths);

            AppSettings actual = await store.LoadAsync();

            Assert.Equal(AppSettings.CurrentSchemaVersion, actual.SchemaVersion);
            Assert.True(actual.Window.AlwaysOnTop);
            Assert.Equal(123.5, actual.Window.Left);
            Assert.False(actual.Media.EnableCloudMusicDetection);
            Assert.Equal("custom-player.exe", actual.Media.TargetProcessName);
            Assert.Equal(400, actual.Media.PollIntervalMilliseconds);
            Assert.Equal(expectedGraceMilliseconds, actual.Media.SilenceGraceMilliseconds);
            Assert.Equal(0.002f, actual.Media.AudiblePeakThreshold);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData(500, 1000)]
    [InlineData(800, 800)]
    public async Task Load_Version2Media_MigratesOnlyTheOldDefaultGracePeriod(
        int storedGraceMilliseconds,
        int expectedGraceMilliseconds)
    {
        string testDirectory = CreateTestDirectory();
        try
        {
            LocalAppPaths paths = new(testDirectory);
            Directory.CreateDirectory(testDirectory);
            await File.WriteAllTextAsync(
                paths.SettingsFile,
                $$"""
                {
                  "schemaVersion": 2,
                  "window": {
                    "alwaysOnTop": true,
                    "top": 456.25
                  },
                  "media": {
                    "enableCloudMusicDetection": true,
                    "targetProcessName": "cloudmusic.exe",
                    "pollIntervalMilliseconds": 250,
                    "silenceGraceMilliseconds": {{storedGraceMilliseconds}},
                    "audiblePeakThreshold": 0.001
                  }
                }
                """);
            JsonSettingsStore store = new(paths);

            AppSettings actual = await store.LoadAsync();

            Assert.Equal(AppSettings.CurrentSchemaVersion, actual.SchemaVersion);
            Assert.True(actual.Window.AlwaysOnTop);
            Assert.Equal(456.25, actual.Window.Top);
            Assert.Equal(expectedGraceMilliseconds, actual.Media.SilenceGraceMilliseconds);
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
    public async Task Load_Version7_PreservesPreferencesAndExtendsOldVolumeFeedbackDefault()
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
                  "schemaVersion": 7,
                  "window": {
                    "alwaysOnTop": true,
                    "left": 321.5
                  },
                  "volume": {
                    "enableMouseWheelControl": false,
                    "enableExternalChangeFeedback": true,
                    "mouseWheelStepPercent": 5,
                    "mergeChangesWithinMilliseconds": 500,
                    "animationCooldownMilliseconds": 2000,
                    "externalPollIntervalMilliseconds": 250
                  }
                }
                """);
            JsonSettingsStore store = new(paths);

            AppSettings actual = await store.LoadAsync();

            Assert.Equal(AppSettings.CurrentSchemaVersion, actual.SchemaVersion);
            Assert.True(actual.Window.AlwaysOnTop);
            Assert.Equal(321.5, actual.Window.Left);
            Assert.False(actual.Window.StartWithWindows);
            Assert.False(actual.Volume.EnableMouseWheelControl);
            Assert.Equal(5, actual.Volume.MouseWheelStepPercent);
            Assert.Equal(1800, actual.Volume.MergeChangesWithinMilliseconds);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Load_Version9_AddsAppearanceDefaultsWithoutLosingExistingPreferences()
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
                  "schemaVersion": 9,
                  "window": {
                    "alwaysOnTop": true,
                    "left": 88.5
                  },
                  "fileTreats": {
                    "enableDesktopFileTreats": false,
                    "maximumQueuedBuns": 4
                  }
                }
                """);
            JsonSettingsStore store = new(paths);

            AppSettings actual = await store.LoadAsync();

            Assert.Equal(AppSettings.CurrentSchemaVersion, actual.SchemaVersion);
            Assert.True(actual.Window.AlwaysOnTop);
            Assert.Equal(88.5, actual.Window.Left);
            Assert.False(actual.FileTreats.EnableDesktopFileTreats);
            Assert.Equal(AppearanceOptionIds.FullBodyLongHair, actual.Appearance.FullBodyStyle);
            Assert.Equal(AppearanceOptionIds.BunEatingOriginal, actual.Appearance.BunEatingStyle);
            Assert.Equal(100, actual.Appearance.DisplayScalePercent);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Load_Version10_AddsRandomMusicAnimationSelection()
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
                  "schemaVersion": 10,
                  "media": {
                    "targetProcessName": "cloudmusic.exe"
                  },
                  "appearance": {
                    "fullBodyStyle": "full-body-classic-cat-ears"
                  }
                }
                """);
            JsonSettingsStore store = new(paths);

            AppSettings actual = await store.LoadAsync();

            Assert.Equal(AppSettings.CurrentSchemaVersion, actual.SchemaVersion);
            Assert.Equal(
                MusicAnimationOptions.RandomSelection,
                actual.Media.MusicAnimationSelection);
            Assert.Equal(
                AppearanceOptionIds.FullBodyClassicCatEars,
                actual.Appearance.FullBodyStyle);
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
