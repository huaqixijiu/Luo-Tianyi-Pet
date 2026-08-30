namespace LuoTianyiPet.Core;

public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 3;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public WindowPreferences Window { get; init; } = new();

    public MediaPreferences Media { get; init; } = new();
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
}
