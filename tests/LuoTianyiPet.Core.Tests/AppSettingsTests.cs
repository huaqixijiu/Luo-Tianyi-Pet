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
        Assert.False(settings.Window.StartWithWindows);
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
        Assert.Equal(350, settings.Media.CommandCooldownMilliseconds);
        Assert.True(settings.Volume.EnableMouseWheelControl);
        Assert.True(settings.Volume.EnableExternalChangeFeedback);
        Assert.Equal(2, settings.Volume.MouseWheelStepPercent);
        Assert.Equal(1800, settings.Volume.MergeChangesWithinMilliseconds);
        Assert.Equal(2000, settings.Volume.AnimationCooldownMilliseconds);
        Assert.Equal(250, settings.Volume.ExternalPollIntervalMilliseconds);
        Assert.True(settings.Genshin.EnableIntegration);
        Assert.Equal("YuanShen.exe;GenshinImpact.exe", settings.Genshin.ProcessNames);
        Assert.Equal(2000, settings.Genshin.StatusPollIntervalMilliseconds);
        Assert.True(settings.Notifications.EnableMessageReminders);
        Assert.False(settings.Notifications.WindowsNotificationAccessGranted);
        Assert.Equal(3000, settings.Notifications.DuplicateWindowMilliseconds);
        Assert.Equal("QQ.exe", settings.Notifications.QqProcessNames);
        Assert.Equal("WeChat.exe;Weixin.exe", settings.Notifications.WeChatProcessNames);
        Assert.Equal(
            "YuanShen.exe;GenshinImpact.exe",
            settings.Safety.ProtectedForegroundProcessNames);
    }
}
