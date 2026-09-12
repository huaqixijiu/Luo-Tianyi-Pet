using System.Runtime.InteropServices;
using LuoTianyiPet.Core;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace LuoTianyiPet.Platform.Windows;

public sealed class WindowsMessageNotificationSource : IMessageNotificationSource, IMessageNotificationDetailSettings
{
    private const ulong MaximumIconBytes = 1024 * 1024;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(750);
    private readonly MessageProviderMatcher _matcher;
    private readonly SemaphoreSlim _pollGate = new(1, 1);
    private readonly NotificationIdSnapshotTracker _snapshotTracker = new();
    private Timer? _pollTimer;
    private bool _started;
    private bool _disposed;
    private volatile bool _qqDetailsEnabled = true;
    private volatile bool _weChatDetailsEnabled = true;
    public void SetWeChatDetailsEnabled(bool enabled)
    {
        _weChatDetailsEnabled = enabled;
        Interlocked.Increment(ref _detailGeneration);
    }
    private bool DetailsEnabled(MessageProvider provider) =>
        provider == MessageProvider.Qq ? _qqDetailsEnabled : _weChatDetailsEnabled;
    private int _detailGeneration;

    public void SetQqDetailsEnabled(bool enabled)
    {
        _qqDetailsEnabled = enabled;
        Interlocked.Increment(ref _detailGeneration);
    }

    public WindowsMessageNotificationSource(MessageProviderMatcher matcher)
    {
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
    }

    public event EventHandler<MessageNotificationReceivedEventArgs>? NotificationReceived;

    public MessageNotificationAccessStatus GetAccessStatus()
    {
        ThrowIfDisposed();
        if (!WindowsPackageIdentity.HasCurrentPackageIdentity())
        {
            return MessageNotificationAccessStatus.PackageIdentityRequired;
        }

        try
        {
            return Map(UserNotificationListener.Current.GetAccessStatus());
        }
        catch (Exception exception) when (IsRecoverablePlatformException(exception))
        {
            return MessageNotificationAccessStatus.Unavailable;
        }
    }

    public async ValueTask<MessageNotificationAccessStatus> RequestAccessAsync()
    {
        ThrowIfDisposed();
        if (!WindowsPackageIdentity.HasCurrentPackageIdentity())
        {
            return MessageNotificationAccessStatus.PackageIdentityRequired;
        }

        try
        {
            UserNotificationListenerAccessStatus status = await UserNotificationListener.Current.RequestAccessAsync();
            return Map(status);
        }
        catch (Exception exception) when (IsRecoverablePlatformException(exception))
        {
            return MessageNotificationAccessStatus.Unavailable;
        }
    }

    public void Start()
    {
        ThrowIfDisposed();
        if (_started || GetAccessStatus() != MessageNotificationAccessStatus.Allowed)
        {
            return;
        }

        // NotificationChanged can raise a non-catchable WinRT dispatcher fault when
        // the Windows notification platform is temporarily unavailable (0x803E0105).
        // Polling the official notification snapshot avoids that unstable event bridge
        // while retaining sub-second QQ/WeChat reminder latency on packaged installs.
        _started = true;
        _pollTimer ??= new Timer(
            _ => _ = PollNotificationsAsync(),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
        _pollTimer.Change(TimeSpan.Zero, PollInterval);
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        _pollTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _snapshotTracker.Reset();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _pollTimer?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private async Task PollNotificationsAsync()
    {
        if (_disposed || !_started || !_pollGate.Wait(0))
        {
            return;
        }

        try
        {
            // The net48 WinRT listener is apartment-bound. Never reuse the UI thread's
            // permission-check instance in this thread-pool poll (RPC_E_WRONG_THREAD).
            UserNotificationListener listener = UserNotificationListener.Current;
            IReadOnlyList<UserNotification> notifications = await listener.GetNotificationsAsync(
                NotificationKinds.Toast);
            if (_disposed || !_started)
            {
                return;
            }

            HashSet<NotificationIdentity> addedIds = _snapshotTracker
                .Observe(notifications.Select(notification => new NotificationIdentity(notification.Id, notification.CreationTime.UtcDateTime.Ticks)))
                .ToHashSet();
            UserNotification[] added = notifications
                .Where(notification => addedIds.Contains(new NotificationIdentity(notification.Id, notification.CreationTime.UtcDateTime.Ticks)))
                .OrderBy(notification => notification.CreationTime)
                .ToArray();
            foreach (UserNotification notification in added)
            {
                try { await ProcessNotificationAsync(notification); }
                catch (Exception) { /* One malformed payload must not skip later notifications in this snapshot. */ }
            }
        }
        catch (Exception exception) when (IsRecoverablePlatformException(exception))
        {
            // The next poll obtains a fresh listener in its own apartment.
        }
        catch (Exception)
        {
            // Malformed third-party notification payloads must not affect the desktop pet.
        }
        finally
        {
            _pollGate.Release();
        }
    }

    private async Task ProcessNotificationAsync(UserNotification notification)
    {
        MessageProvider? provider = _matcher.Identify(
            notification.AppInfo.AppUserModelId,
            notification.AppInfo.DisplayInfo.DisplayName);
        if (provider is not MessageProvider matched)
        {
            return;
        }

        int generation = Volatile.Read(ref _detailGeneration);
        bool readDetails = DetailsEnabled(matched);
        var details = readDetails ? TryReadDetails(notification, true) : (Title: (string?)null, Preview: (string?)null);
        byte[]? applicationIcon = await TryReadApplicationIconAsync(notification);
        if (_disposed || !_started)
        {
            return;
        }

        if (!DetailsEnabled(matched) || generation != Volatile.Read(ref _detailGeneration))
            details = (null, null);

        NotificationReceived?.Invoke(
            this,
            new MessageNotificationReceivedEventArgs(
                new MessageNotificationSummary(
                    matched,
                    notification.CreationTime,
                    details.Title,
                    applicationIcon,
                    ContactAvatar: null,
                    MessagePreview: details.Preview,
                    NotificationKey: $"{notification.Id}:{notification.CreationTime.UtcDateTime.Ticks}")));
    }

    private static (string? Title, string? Preview) TryReadDetails(UserNotification notification, bool includePreview)
    {
        NotificationBinding? binding = notification.Notification.Visual.GetBinding(
            KnownNotificationBindings.ToastGeneric) ??
            notification.Notification.Visual.Bindings.FirstOrDefault();
        if (binding is null)
        {
            return (null, null);
        }

        IReadOnlyList<AdaptiveNotificationText> elements = binding.GetTextElements();
        return NotificationTextSelector.Read(elements.Count, index => elements[index].Text, true, includePreview);
    }

    private static async Task<byte[]?> TryReadApplicationIconAsync(UserNotification notification)
    {
        try
        {
            RandomAccessStreamReference? logoReference = notification.AppInfo.DisplayInfo.GetLogo(
                new Size(48, 48));
            // QQ may provide a valid title with no logo. The optional image must not
            // discard that notification's text metadata.
            if (logoReference is null) return null;
            using IRandomAccessStreamWithContentType stream = await logoReference.OpenReadAsync();
            if (stream.Size is 0 or > MaximumIconBytes)
            {
                return null;
            }

            uint byteCount = checked((uint)stream.Size);
            using DataReader reader = new(stream.GetInputStreamAt(0));
            uint loaded = await reader.LoadAsync(byteCount);
            if (loaded == 0)
            {
                return null;
            }

            byte[] bytes = new byte[loaded];
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch (Exception exception) when (
            IsRecoverablePlatformException(exception) ||
            exception is IOException or ArgumentException or OverflowException)
        {
            return null;
        }
    }

    private static MessageNotificationAccessStatus Map(
        UserNotificationListenerAccessStatus status) => status switch
    {
        UserNotificationListenerAccessStatus.Allowed => MessageNotificationAccessStatus.Allowed,
        UserNotificationListenerAccessStatus.Denied => MessageNotificationAccessStatus.Denied,
        UserNotificationListenerAccessStatus.Unspecified => MessageNotificationAccessStatus.Unspecified,
        _ => MessageNotificationAccessStatus.Unavailable,
    };

    internal static bool IsRecoverablePlatformException(Exception exception) =>
        exception is UnauthorizedAccessException or COMException or InvalidOperationException ||
        exception.HResult is unchecked((int)0x800706BA) or
            unchecked((int)0x800706BE) or
            unchecked((int)0x80010108) or
            unchecked((int)0x8001010E) or
            unchecked((int)0x803E0105);

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WindowsMessageNotificationSource));
        }
    }
}

internal readonly record struct NotificationIdentity(uint Id, long CreatedAtTicks);

internal sealed class NotificationIdSnapshotTracker
{
    private readonly object _sync = new();
    private readonly HashSet<NotificationIdentity> _knownIds = new();
    private bool _hasBaseline;

    public IReadOnlyList<uint> Observe(IEnumerable<uint> notificationIds) =>
        Observe(notificationIds.Select(id => new NotificationIdentity(id, 0))).Select(item => item.Id).ToArray();

    public IReadOnlyList<NotificationIdentity> Observe(IEnumerable<NotificationIdentity> notificationIds)
    {
        if (notificationIds is null)
        {
            throw new ArgumentNullException(nameof(notificationIds));
        }
        lock (_sync)
        {
            HashSet<NotificationIdentity> currentIds = notificationIds.ToHashSet();
            NotificationIdentity[] addedIds = _hasBaseline
                ? currentIds.Where(id => !_knownIds.Contains(id)).ToArray()
                : Array.Empty<NotificationIdentity>();

            _knownIds.Clear();
            _knownIds.UnionWith(currentIds);
            _hasBaseline = true;
            return addedIds;
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _knownIds.Clear();
            _hasBaseline = false;
        }
    }
}

internal static class NotificationConversationTitleSelector
{
    private const int MaximumDisplayLength = 64;

    public static string? Select(int textElementCount, string? firstText)
    {
        // A single text element may itself be message content. Only accept the first
        // element when a separate body element exists.
        if (textElementCount < 2 || string.IsNullOrWhiteSpace(firstText))
        {
            return null;
        }

        string normalized = string.Concat(firstText!
            .Trim()
            .Where(character => !char.IsControl(character)));
        if (normalized.Length == 0)
        {
            return null;
        }

        return normalized.Length <= MaximumDisplayLength
            ? normalized
            : normalized.Substring(0, MaximumDisplayLength - 1) + "…";
    }
}

internal static class WindowsPackageIdentity
{
    private const int ErrorInsufficientBuffer = 122;
    public static bool HasCurrentPackageIdentity()
    {
        uint length = 0;
        int result = GetCurrentPackageFullName(ref length, null);
        return result == ErrorInsufficientBuffer && length > 0;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(
        ref uint packageFullNameLength,
        char[]? packageFullName);
}

internal static class NotificationTextSelector
{
    public static (string? Title, string? Preview) Read(int count, Func<int, string?> getText,
        bool detailsEnabled, bool includePreview)
    {
        if (!detailsEnabled || count < 2) return (null, null);
        string? title = NotificationConversationTitleSelector.Select(count, getText(0));
        // Reject ambiguous payloads instead of putting a body in the sender slot.
        if (title is null || !includePreview) return (title, null);
        string preview;
        try
        {
            preview = string.Join(" ", Enumerable.Range(1, Math.Min(count - 1, 3))
                .Select(i => getText(i) ?? string.Empty));
        }
        catch (Exception) { return (title, null); }
        preview = System.Text.RegularExpressions.Regex.Replace(preview, @"\s+", " ").Trim();
        preview = string.Concat(preview.Where(c => !char.IsControl(c) &&
            System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Format));
        if (preview.Length > 120)
        {
            int length = char.IsHighSurrogate(preview[118]) ? 118 : 119;
            preview = preview.Substring(0, length) + "…";
        }
        return (title, preview.Length == 0 ? null : preview);
    }
}
