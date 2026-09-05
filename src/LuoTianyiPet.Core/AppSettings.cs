namespace LuoTianyiPet.Core;

public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 12;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public WindowPreferences Window { get; init; } = new();

    public MediaPreferences Media { get; init; } = new();

    public VolumePreferences Volume { get; init; } = new();

    public GenshinPreferences Genshin { get; init; } = new();

    public MessageNotificationPreferences Notifications { get; init; } = new();

    public FileTreatPreferences FileTreats { get; init; } = new();

    public AppearancePreferences Appearance { get; init; } = new();

    public SafetyPreferences Safety { get; init; } = new();
}

public static class AppearanceOptionIds
{
    public const string FullBodyLongHair = "full-body-long-hair";
    public const string FullBodyCrystalDress = "full-body-crystal-dress";
    public const string FullBodyClassicCatEars = "full-body-classic-cat-ears";
    public const string BunEatingOriginal = "bun-eating-original";
    public const string BunEatingNew = "bun-eating-new";

    public const string LongHairAnimation = "official-v4-chibi-full-body-idle";
    public const string CrystalDressAnimation = "user-chibi-crystal-full-body-idle";
    public const string ClassicCatEarsAnimation = "user-chibi-classic-full-body-idle";
    public const string OriginalBunRunAnimation = "ai-bun-chase-run";
    public const string OriginalBunEatAnimation = "ai-bun-eat";
    public const string NewBunRunAnimation = "ai-bun-v2-chase-run";
    public const string NewBunEatAnimation = "ai-bun-v2-eat";

    public static string NormalizeFullBodyStyle(string? value) => value switch
    {
        FullBodyLongHair or FullBodyCrystalDress or FullBodyClassicCatEars => value,
        _ => FullBodyLongHair,
    };

    public static string NormalizeBunEatingStyle(string? value) => value switch
    {
        BunEatingOriginal or BunEatingNew => value,
        _ => BunEatingOriginal,
    };

    public static string GetNextFullBodyStyle(string? style) =>
        NormalizeFullBodyStyle(style) switch
        {
            FullBodyLongHair => FullBodyCrystalDress,
            FullBodyCrystalDress => FullBodyClassicCatEars,
            _ => FullBodyLongHair,
        };

    public static string ResolveDefaultBunEatingStyle(string? fullBodyStyle) =>
        NormalizeFullBodyStyle(fullBodyStyle) == FullBodyClassicCatEars
            ? BunEatingOriginal
            : BunEatingNew;

    public static string ResolveFullBodyAnimation(string? style) =>
        NormalizeFullBodyStyle(style) switch
        {
            FullBodyCrystalDress => CrystalDressAnimation,
            FullBodyClassicCatEars => ClassicCatEarsAnimation,
            _ => LongHairAnimation,
        };

    public static (string RunAnimation, string EatAnimation) ResolveBunAnimations(string? style) =>
        NormalizeBunEatingStyle(style) == BunEatingNew
            ? (NewBunRunAnimation, NewBunEatAnimation)
            : (OriginalBunRunAnimation, OriginalBunEatAnimation);

    public static FullBodyInteractionMode ResolveFullBodyInteractionMode(string? style) =>
        NormalizeFullBodyStyle(style) switch
        {
            FullBodyCrystalDress => FullBodyInteractionMode.SeamlessMotion,
            FullBodyClassicCatEars => FullBodyInteractionMode.ExpressionPack,
            _ => FullBodyInteractionMode.Disabled,
        };

    public static bool HasFullBodyInteractions(string? style) =>
        ResolveFullBodyInteractionMode(style) != FullBodyInteractionMode.Disabled;

    public static bool UsesExpansionDragAnimation(string? style) =>
        NormalizeFullBodyStyle(style) == FullBodyClassicCatEars;
}

public enum FullBodyInteractionMode
{
    Disabled,
    SeamlessMotion,
    ExpressionPack,
}

public sealed record AppearancePreferences
{
    public const int MinimumDisplayScalePercent = 50;
    public const int MaximumDisplayScalePercent = 200;
    public const int DefaultDisplayScalePercent = 100;

    public string FullBodyStyle { get; init; } = AppearanceOptionIds.FullBodyLongHair;

    public string BunEatingStyle { get; init; } = AppearanceOptionIds.BunEatingNew;

    public bool EnableFullBodyStyleCycling { get; init; } = true;

    public int DisplayScalePercent { get; init; } = DefaultDisplayScalePercent;

    public static AppearancePreferences Normalize(AppearancePreferences? preferences)
    {
        preferences ??= new AppearancePreferences();
        string fullBodyStyle = AppearanceOptionIds.NormalizeFullBodyStyle(
            preferences.FullBodyStyle);
        return preferences with
        {
            FullBodyStyle = fullBodyStyle,
            BunEatingStyle = AppearanceOptionIds.ResolveDefaultBunEatingStyle(fullBodyStyle),
            DisplayScalePercent = Math.Clamp(
                preferences.DisplayScalePercent,
                MinimumDisplayScalePercent,
                MaximumDisplayScalePercent),
        };
    }
}

public sealed record FileTreatPreferences
{
    public bool EnableDesktopFileTreats { get; init; } = true;

    public int MaximumQueuedBuns { get; init; } = 6;
}

public sealed record MessageNotificationPreferences
{
    public const int DefaultDuplicateWindowMilliseconds = 3000;

    public bool EnableMessageReminders { get; init; } = true;

    public bool WindowsNotificationAccessGranted { get; init; }

    public int DuplicateWindowMilliseconds { get; init; } =
        DefaultDuplicateWindowMilliseconds;

    public string QqApplicationIdentifiers { get; init; } = "QQ;TencentQQ;Tencent.QQ";

    public string WeChatApplicationIdentifiers { get; init; } =
        "微信;WeChat;Weixin;Tencent.WeChat;Tencent.Weixin";

    public string QqProcessNames { get; init; } = "QQ.exe";

    public string WeChatProcessNames { get; init; } = "WeChat.exe;Weixin.exe";
}

public sealed record GenshinPreferences
{
    public const int DefaultStatusPollIntervalMilliseconds = 2000;

    public bool EnableIntegration { get; init; } = true;

    public string ProcessNames { get; init; } = "YuanShen.exe;GenshinImpact.exe";

    public int StatusPollIntervalMilliseconds { get; init; } =
        DefaultStatusPollIntervalMilliseconds;
}

public sealed record VolumePreferences
{
    public const int DefaultMouseWheelStepPercent = 2;
    public const int DefaultMergeChangesWithinMilliseconds = 1800;
    public const int DefaultAnimationCooldownMilliseconds = 2000;
    public const int DefaultExternalPollIntervalMilliseconds = 250;

    public bool EnableMouseWheelControl { get; init; } = true;

    public bool EnableExternalChangeFeedback { get; init; } = true;

    public int MouseWheelStepPercent { get; init; } = DefaultMouseWheelStepPercent;

    public int MergeChangesWithinMilliseconds { get; init; } =
        DefaultMergeChangesWithinMilliseconds;

    public int AnimationCooldownMilliseconds { get; init; } =
        DefaultAnimationCooldownMilliseconds;

    public int ExternalPollIntervalMilliseconds { get; init; } =
        DefaultExternalPollIntervalMilliseconds;
}

public sealed record WindowPreferences
{
    public bool AlwaysOnTop { get; init; }

    public bool StartWithWindows { get; init; }

    public double? Left { get; init; }

    public double? Top { get; init; }
}

public sealed record MediaPreferences
{
    public const int DefaultPollIntervalMilliseconds = 250;
    public const int DefaultSilenceGraceMilliseconds = 1000;
    public const float DefaultAudiblePeakThreshold = 0.001f;

    public bool EnableCloudMusicDetection { get; init; } = true;

    public string TargetProcessName { get; init; } = "cloudmusic.exe";

    public int PollIntervalMilliseconds { get; init; } = DefaultPollIntervalMilliseconds;

    public int SilenceGraceMilliseconds { get; init; } = DefaultSilenceGraceMilliseconds;

    public float AudiblePeakThreshold { get; init; } = DefaultAudiblePeakThreshold;

    public bool EnableCloudMusicShortcutControl { get; init; } = true;

    public string PreviousTrackShortcut { get; init; } = "Ctrl+Alt+Left";

    public string TogglePlayPauseShortcut { get; init; } = "Ctrl+Alt+P";

    public string NextTrackShortcut { get; init; } = "Ctrl+Alt+Right";

    public int CommandCooldownMilliseconds { get; init; } = 350;

    public string MusicAnimationSelection { get; init; } =
        MusicAnimationOptions.RandomSelection;

    public static MediaPreferences Normalize(MediaPreferences? preferences)
    {
        preferences ??= new MediaPreferences();
        return preferences with
        {
            MusicAnimationSelection = MusicAnimationOptions.NormalizeSelection(
                preferences.MusicAnimationSelection),
        };
    }
}

public sealed record SafetyPreferences
{
    public string ProtectedForegroundProcessNames { get; init; } =
        "YuanShen.exe;GenshinImpact.exe";
}
