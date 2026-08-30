using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void Defaults_AreSafeAndNotTopmost()
    {
        AppSettings settings = new();

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.False(settings.Window.AlwaysOnTop);
        Assert.Null(settings.Window.Left);
        Assert.Null(settings.Window.Top);
        Assert.True(settings.Media.EnableCloudMusicDetection);
        Assert.Equal("cloudmusic.exe", settings.Media.TargetProcessName);
        Assert.Equal(250, settings.Media.PollIntervalMilliseconds);
        Assert.Equal(1000, settings.Media.SilenceGraceMilliseconds);
        Assert.Equal(0.001f, settings.Media.AudiblePeakThreshold);
        Assert.True(settings.Media.EnableCloudMusicShortcutControl);
        Assert.Equal("Ctrl+Alt+Left", settings.Media.PreviousTrackShortcut);
        Assert.Equal("Ctrl+Alt+P", settings.Media.TogglePlayPauseShortcut);
        Assert.Equal("Ctrl+Alt+Right", settings.Media.NextTrackShortcut);
        Assert.Equal("Ctrl+Alt+L", settings.Media.FavoriteTrackShortcut);
        Assert.Equal(350, settings.Media.CommandCooldownMilliseconds);
        Assert.Equal(
            "YuanShen.exe;GenshinImpact.exe",
            settings.Safety.ProtectedForegroundProcessNames);
    }
}
