namespace LuoTianyiPet.Core;

/// <summary>User-facing operation messages; platform diagnostics stay in structured logs.</summary>
public static class OperationFeedback
{
    public static readonly TimeSpan FailureDuration = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan BriefDuration = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan ProgressDelay = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan RepeatCooldown = TimeSpan.FromSeconds(8);

    public const string LaunchStarting = "正在启动网易云音乐…";
    public const string LaunchRetrying = "启动有点慢，正在继续尝试…";
    public const string PlaybackWaiting = "网易云已打开，正在等待播放…";
    public const string PlayerNotFound = "没有找到网易云音乐，请确认已经安装";
    public const string MediaEnvironmentUnavailable = "当前环境无法自动控制网易云，请手动操作";
    public const string LaunchFailed = "网易云暂时无法启动，请稍后重试";
    public const string PlaybackUnconfirmed = "网易云已打开，但未检测到播放，请在网易云中检查";
    public const string ShortcutUnavailable = "媒体快捷键不可用，请检查设置";
    public const string KeyboardBusy = "请松开键盘按键后重试";
    public const string MediaControlFailed = "媒体控制未生效，请再试一次";
    public const string VolumeNeedsPlayback = "请先播放一首歌，再调节网易云音量";
    public const string VolumeFailed = "暂时无法调整网易云音量，请稍后重试";
    public const string DropReady = "松开放入回收站";
    public const string RecycleCancelled = "已取消，文件仍在原处";
    public const string RecycleFailed = "未能放入回收站，文件仍在原处";
    public const string RecycleUnconfirmed = "回收结果未能确认，请检查原位置和回收站";
    public const string BunQueueFull = "包子太多啦，先吃完这些吧";
    public const string StartupSettingFailed = "开机自启动设置失败，请稍后重试";
    public const string SettingsSaveFailed = "设置未能保存，请稍后重试";

    public static string? ForMediaCommand(MediaCommandSendStatus status) => status switch
    {
        MediaCommandSendStatus.Sent or MediaCommandSendStatus.RateLimited => null,
        MediaCommandSendStatus.Disabled or MediaCommandSendStatus.InvalidShortcut => ShortcutUnavailable,
        MediaCommandSendStatus.ProtectedApplicationForeground or
            MediaCommandSendStatus.ForegroundCheckUnavailable => MediaEnvironmentUnavailable,
        MediaCommandSendStatus.KeyboardBusy => KeyboardBusy,
        _ => MediaControlFailed,
    };

    public static string? ForLaunch(MediaApplicationLaunchStatus status) => status switch
    {
        MediaApplicationLaunchStatus.Started or MediaApplicationLaunchStatus.AlreadyRunning => null,
        MediaApplicationLaunchStatus.NotFound => PlayerNotFound,
        MediaApplicationLaunchStatus.ProtectedApplicationForeground or
            MediaApplicationLaunchStatus.ForegroundCheckUnavailable => MediaEnvironmentUnavailable,
        _ => LaunchFailed,
    };

    public static string? ForVolume(ApplicationVolumeAdjustmentStatus status) => status switch
    {
        ApplicationVolumeAdjustmentStatus.Succeeded or ApplicationVolumeAdjustmentStatus.AtLimit => null,
        ApplicationVolumeAdjustmentStatus.TargetSessionMissing => VolumeNeedsPlayback,
        _ => VolumeFailed,
    };

    public static string? LaunchProgress(TimeSpan elapsed, int attempts, TimeSpan? sincePlayCommand)
    {
        if (elapsed < ProgressDelay)
        {
            return null;
        }

        return sincePlayCommand >= ProgressDelay
            ? PlaybackWaiting
            : attempts > 1 ? LaunchRetrying : LaunchStarting;
    }

    public static bool IsLaunchProgress(string text) =>
        text is LaunchStarting or LaunchRetrying or PlaybackWaiting;

    public static string? ForRecycle(RecycleBinOperationResult result)
    {
        if (result.Succeeded)
        {
            return null;
        }

        // A failed/aborted shell operation may already have moved some items.
        // The platform's count is based on missing source paths, not confirmed destinations.
        if (result.RecycledCount > 0)
        {
            return result.Status == RecycleBinOperationStatus.PartialFailure &&
                result.RecycledCount < result.RequestedCount
                ? $"仅 {result.RecycledCount} 个项目进入回收站，请检查其余文件"
                : RecycleUnconfirmed;
        }

        return result.Status switch
        {
            RecycleBinOperationStatus.Cancelled => RecycleCancelled,
            RecycleBinOperationStatus.Rejected => result.Message,
            _ => RecycleFailed,
        };
    }
}

/// <summary>Suppresses duplicates without extending either visibility or the cooldown.</summary>
public sealed class FeedbackRepeatGate
{
    private readonly Dictionary<string, DateTimeOffset> _shownAt = new(StringComparer.Ordinal);

    public bool TryShow(string message, DateTimeOffset now)
    {
        foreach (string key in _shownAt.Where(entry =>
            now - entry.Value >= OperationFeedback.RepeatCooldown || now < entry.Value)
            .Select(entry => entry.Key).ToArray())
        {
            _shownAt.Remove(key);
        }

        if (_shownAt.ContainsKey(message))
        {
            return false;
        }

        _shownAt[message] = now;
        return true;
    }
}
