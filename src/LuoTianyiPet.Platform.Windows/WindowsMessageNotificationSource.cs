using System.Runtime.InteropServices;
using LuoTianyiPet.Core;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace LuoTianyiPet.Platform.Windows;

public sealed class WindowsMessageNotificationSource : IMessageNotificationSource
{
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

    private void OnNotificationChanged(
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
                NotificationReceived?.Invoke(
                    this,
                    new MessageNotificationReceivedEventArgs(
                        matched,
                        notification.CreationTime));
            }
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or COMException or InvalidOperationException)
        {
            // Permission may be revoked while the app is running. The next status check
            // reports the unavailable state; notification contents are never inspected.
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
