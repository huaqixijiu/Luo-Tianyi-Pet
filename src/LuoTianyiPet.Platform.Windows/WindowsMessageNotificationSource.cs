using System.Runtime.InteropServices;
using LuoTianyiPet.Core;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace LuoTianyiPet.Platform.Windows;

public sealed class WindowsMessageNotificationSource : IMessageNotificationSource
{
    private const ulong MaximumIconBytes = 1024 * 1024;
    private readonly MessageProviderMatcher _matcher;
    private UserNotificationListener? _listener;
    private bool _started;
    private bool _disposed;

    public WindowsMessageNotificationSource(MessageProviderMatcher matcher)
    {
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
    }

    public event EventHandler<MessageNotificationReceivedEventArgs>? NotificationReceived;

    public MessageNotificationAccessStatus GetAccessStatus()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!WindowsPackageIdentity.HasCurrentPackageIdentity())
        {
            return MessageNotificationAccessStatus.PackageIdentityRequired;
        }

        try
        {
            _listener ??= UserNotificationListener.Current;
            return Map(_listener.GetAccessStatus());
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or COMException or InvalidOperationException)
        {
            return MessageNotificationAccessStatus.Unavailable;
        }
    }

    public async ValueTask<MessageNotificationAccessStatus> RequestAccessAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!WindowsPackageIdentity.HasCurrentPackageIdentity())
        {
            return MessageNotificationAccessStatus.PackageIdentityRequired;
        }

        try
        {
            _listener ??= UserNotificationListener.Current;
            UserNotificationListenerAccessStatus status = await _listener.RequestAccessAsync();
            return Map(status);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or COMException or InvalidOperationException)
        {
            return MessageNotificationAccessStatus.Unavailable;
        }
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started || GetAccessStatus() != MessageNotificationAccessStatus.Allowed)
        {
            return;
        }

        try
        {
            _listener!.NotificationChanged += OnNotificationChanged;
            _started = true;
        }
        catch (Exception exception) when (IsRecoverablePlatformException(exception))
        {
            // The Windows notification RPC service may be restarting even though the
            // cached access status is Allowed. Treat this as temporarily unavailable;
            // the settings/status refresh can establish a fresh listener later.
            _listener = null;
            _started = false;
        }
    }

    public void Stop()
    {
        if (!_started || _listener is null)
        {
            return;
        }

        try
        {
            _listener.NotificationChanged -= OnNotificationChanged;
        }
        catch (Exception exception) when (IsRecoverablePlatformException(exception))
        {
            // A disconnected Windows notification service has no live subscription
            // left to remove.
        }
        _started = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private async void OnNotificationChanged(
        UserNotificationListener sender,
        UserNotificationChangedEventArgs args)
    {
        if (_disposed || args.ChangeKind != UserNotificationChangedKind.Added)
        {
            return;
        }

        try
        {
            UserNotification? notification = sender.GetNotification(args.UserNotificationId);
            if (notification is null)
            {
                return;
            }

            MessageProvider? provider = _matcher.Identify(
                notification.AppInfo.AppUserModelId,
                notification.AppInfo.DisplayInfo.DisplayName);
            if (provider is MessageProvider matched)
            {
                string? conversationDisplayName = TryReadConversationDisplayName(notification);
                byte[]? applicationIcon = await TryReadApplicationIconAsync(notification);
                if (_disposed)
                {
                    return;
                }

                NotificationReceived?.Invoke(
                    this,
                    new MessageNotificationReceivedEventArgs(
                        new MessageNotificationSummary(
                            matched,
                            notification.CreationTime,
                            conversationDisplayName,
                            applicationIcon,
                            ContactAvatar: null)));
            }
        }
        catch (Exception)
        {
            // Permission may be revoked while the app is running. The next status check
            // reports the unavailable state. Platform payload failures are isolated here
            // because this is an async WinRT event boundary. Nothing is persisted.
        }
    }

    private static string? TryReadConversationDisplayName(UserNotification notification)
    {
        NotificationBinding? binding = notification.Notification.Visual.GetBinding(
            KnownNotificationBindings.ToastGeneric) ??
            notification.Notification.Visual.Bindings.FirstOrDefault();
        if (binding is null)
        {
            return null;
        }

        IReadOnlyList<AdaptiveNotificationText> elements = binding.GetTextElements();
        string? firstText = elements.Count > 0 ? elements[0].Text : null;
        return NotificationConversationTitleSelector.Select(elements.Count, firstText);
    }

    private static async Task<byte[]?> TryReadApplicationIconAsync(UserNotification notification)
    {
        try
        {
            RandomAccessStreamReference logoReference = notification.AppInfo.DisplayInfo.GetLogo(
                new Size(48, 48));
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

    private static bool IsRecoverablePlatformException(Exception exception) =>
        exception is UnauthorizedAccessException or COMException or InvalidOperationException;
}

internal static class NotificationConversationTitleSelector
{
    private const int MaximumDisplayLength = 64;

    public static string? Select(int textElementCount, string? firstText)
    {
        // A single text element may itself be message content. Only accept the first
        // element when a separate body element exists, and never read that body value.
        if (textElementCount < 2 || string.IsNullOrWhiteSpace(firstText))
        {
            return null;
        }

        string normalized = string.Concat(firstText
            .Trim()
            .Where(character => !char.IsControl(character)));
        if (normalized.Length == 0)
        {
            return null;
        }

        return normalized.Length <= MaximumDisplayLength
            ? normalized
            : normalized[..(MaximumDisplayLength - 1)] + "…";
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
