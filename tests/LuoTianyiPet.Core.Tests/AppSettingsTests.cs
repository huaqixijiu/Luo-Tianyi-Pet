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
        Assert.Equal(MusicAnimationOptions.RandomSelection, settings.Media.MusicAnimationSelection);
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
        Assert.True(settings.FileTreats.EnableDesktopFileTreats);
        Assert.Equal(6, settings.FileTreats.MaximumQueuedBuns);
        Assert.Equal(AppearanceOptionIds.FullBodyLongHair, settings.Appearance.FullBodyStyle);
        Assert.Equal(AppearanceOptionIds.BunEatingNew, settings.Appearance.BunEatingStyle);
        Assert.True(settings.Appearance.EnableFullBodyStyleCycling);
        Assert.Equal(100, settings.Appearance.DisplayScalePercent);
        Assert.Equal(
            "YuanShen.exe;GenshinImpact.exe",
            settings.Safety.ProtectedForegroundProcessNames);
    }

    [Theory]
    [InlineData(10, AppearancePreferences.MinimumDisplayScalePercent)]
    [InlineData(125, 125)]
    [InlineData(250, AppearancePreferences.MaximumDisplayScalePercent)]
    public void AppearanceNormalization_ClampsScaleAndRejectsUnknownStyles(
        int storedScale,
        int expectedScale)
    {
        AppearancePreferences normalized = AppearancePreferences.Normalize(new AppearancePreferences
        {
            FullBodyStyle = "unknown-character",
            BunEatingStyle = "unknown-bun",
            DisplayScalePercent = storedScale,
        });

        Assert.Equal(AppearanceOptionIds.FullBodyLongHair, normalized.FullBodyStyle);
        Assert.Equal(AppearanceOptionIds.BunEatingNew, normalized.BunEatingStyle);
        Assert.Equal(expectedScale, normalized.DisplayScalePercent);
    }

    [Theory]
    [InlineData(AppearanceOptionIds.FullBodyLongHair, AppearanceOptionIds.FullBodyCrystalDress)]
    [InlineData(AppearanceOptionIds.FullBodyCrystalDress, AppearanceOptionIds.FullBodyClassicCatEars)]
    [InlineData(AppearanceOptionIds.FullBodyClassicCatEars, AppearanceOptionIds.FullBodyLongHair)]
    [InlineData("unknown", AppearanceOptionIds.FullBodyCrystalDress)]
    public void AppearanceOptions_CycleInTheUserFacingOrder(string current, string expected)
    {
        Assert.Equal(expected, AppearanceOptionIds.GetNextFullBodyStyle(current));
    }

    [Theory]
    [InlineData(AppearanceOptionIds.FullBodyLongHair, AppearanceOptionIds.BunEatingNew)]
    [InlineData(AppearanceOptionIds.FullBodyCrystalDress, AppearanceOptionIds.BunEatingNew)]
    [InlineData(AppearanceOptionIds.FullBodyClassicCatEars, AppearanceOptionIds.BunEatingOriginal)]
    public void AppearanceOptions_DeriveBunStyleFromTheCurrentCharacter(
        string fullBodyStyle,
        string expectedBunStyle)
    {
        Assert.Equal(
            expectedBunStyle,
            AppearanceOptionIds.ResolveDefaultBunEatingStyle(fullBodyStyle));

        AppearancePreferences normalized = AppearancePreferences.Normalize(
            new AppearancePreferences
            {
                FullBodyStyle = fullBodyStyle,
                BunEatingStyle = AppearanceOptionIds.BunEatingOriginal,
            });
        Assert.Equal(expectedBunStyle, normalized.BunEatingStyle);
    }

    [Fact]
    public void AppearanceOptions_ResolveAllSelectableRuntimeAnimations()
    {
        Assert.Equal(
            AppearanceOptionIds.CrystalDressAnimation,
            AppearanceOptionIds.ResolveFullBodyAnimation(AppearanceOptionIds.FullBodyCrystalDress));
        Assert.Equal(
            AppearanceOptionIds.ClassicCatEarsAnimation,
            AppearanceOptionIds.ResolveFullBodyAnimation(AppearanceOptionIds.FullBodyClassicCatEars));
        Assert.Equal(
            (AppearanceOptionIds.NewBunRunAnimation, AppearanceOptionIds.NewBunEatAnimation),
            AppearanceOptionIds.ResolveBunAnimations(AppearanceOptionIds.BunEatingNew));
        Assert.Equal(
            FullBodyInteractionMode.Disabled,
            AppearanceOptionIds.ResolveFullBodyInteractionMode(
                AppearanceOptionIds.FullBodyLongHair));
        Assert.Equal(
            FullBodyInteractionMode.SeamlessMotionPending,
            AppearanceOptionIds.ResolveFullBodyInteractionMode(
                AppearanceOptionIds.FullBodyCrystalDress));
        Assert.Equal(
            FullBodyInteractionMode.ExpressionPack,
            AppearanceOptionIds.ResolveFullBodyInteractionMode(
                AppearanceOptionIds.FullBodyClassicCatEars));
    }

    [Theory]
    [InlineData(AppearanceOptionIds.FullBodyLongHair, false)]
    [InlineData(AppearanceOptionIds.FullBodyCrystalDress, false)]
    [InlineData(AppearanceOptionIds.FullBodyClassicCatEars, true)]
    [InlineData("unknown", false)]
    public void AppearanceOptions_UseExpansionDragOnlyForClassicCatEars(
        string fullBodyStyle,
        bool expected)
    {
        Assert.Equal(
            expected,
            AppearanceOptionIds.UsesExpansionDragAnimation(fullBodyStyle));
    }

    [Theory]
    [InlineData("random", "random")]
    [InlineData(PetVisualState.EnjoyMusicAnimation, PetVisualState.EnjoyMusicAnimation)]
    [InlineData(PetVisualState.MusicSwayAnimation, PetVisualState.MusicSwayAnimation)]
    [InlineData("unknown", "random")]
    public void MediaNormalizationPreservesOnlyRegisteredMusicSelections(
        string stored,
        string expected)
    {
        MediaPreferences normalized = MediaPreferences.Normalize(new MediaPreferences
        {
            MusicAnimationSelection = stored,
        });

        Assert.Equal(expected, normalized.MusicAnimationSelection);
    }
}
