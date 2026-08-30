namespace LuoTianyiPet.Core;

public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 7;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public WindowPreferences Window { get; init; } = new();

    public MediaPreferences Media { get; init; } = new();

    public VolumePreferences Volume { get; init; } = new();

    public GenshinPreferences Genshin { get; init; } = new();

    public MessageNotificationPreferences Notifications { get; init; } = new();

    public SafetyPreferences Safety { get; init; } = new();
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
    public const int DefaultMergeChangesWithinMilliseconds = 500;
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

    public string FavoriteTrackShortcut { get; init; } = "Ctrl+Alt+L";

    public int CommandCooldownMilliseconds { get; init; } = 350;
}

public sealed record SafetyPreferences
{
    public string ProtectedForegroundProcessNames { get; init; } =
        "YuanShen.exe;GenshinImpact.exe";
}
