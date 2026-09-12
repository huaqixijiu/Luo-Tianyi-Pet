using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private readonly MessageNotificationCounter _messageNotificationCounter = new();
    private WindowsWeChatSessionNotificationSource? _weChatSessionSource;
    private void UpdateWeChatMonitoring()
    {
        var foreground = _foregroundApplicationProbe?.Query();
        bool enabled = foreground?.Succeeded == true && foreground?.IsFullscreen == false && _persistSettings && !_isClosing && !_systemSessionUnavailable &&
            _settings.Notifications.EnableMessageReminders && _settings.Notifications.EnableWeChatDetailedReminders;
        if (!enabled) { _weChatSessionSource?.Stop(); return; }
        if (_weChatSessionSource is null)
        {
            _weChatSessionSource = new();
            _weChatSessionSource.NotificationReceived += OnMessageNotificationReceived;
            _weChatSessionSource.SnapshotChanged += (_, _) => Dispatcher.BeginInvoke(PruneMessageInbox);
        }
        _weChatSessionSource.Start();
    }
    private DateTimeOffset _lastWeChatForegroundAt = DateTimeOffset.MinValue;
    private long _weChatDetailRevision;

    private bool IsWeChatReminderCurrent(MessageNotificationSummary notification) =>
        notification.WeChatSessionKey is null
            ? _weChatSessionSource?.HasUnreadConversations != false
            : _weChatSessionSource?.IsCurrent(notification) == true;

    private bool CanPresentWeChatReminder(MessageNotificationSummary notification) =>
        notification.Provider != MessageProvider.WeChat ||
        (WeChatReminderFreshness.CanPresent(notification, DateTimeOffset.Now, _lastWeChatForegroundAt) &&
            IsWeChatReminderCurrent(notification));

    private void DiscardReadWeChatReminders()
    {
        if (_isClosing) return;
        _messageNotificationCoordinator.DiscardPending(notification => !CanPresentWeChatReminder(notification));
        if (_displayedMessageSummary is { Provider: MessageProvider.WeChat } active &&
            !IsWeChatReminderCurrent(active))
            CancelMessageNotificationPresentation(restoreContinuousAnimation: true);
    }

    private async Task HandleWeChatAttentionAsync(DateTimeOffset occurredAt)
    {
        long revision = _weChatDetailRevision;
        _weChatSessionSource?.RequestRefresh();
        await Task.Delay(450);
        _weChatSessionSource?.RequestRefresh();
        await Task.Delay(450);
        // Give the public session fields a short opportunity to catch up with the Shell signal.
        // A detailed event owns this reminder once received; never follow it with another generic card.
        var notification = new MessageNotificationSummary(MessageProvider.WeChat, occurredAt);
        if (!_isClosing && revision == _weChatDetailRevision && CanPresentWeChatReminder(notification))
            HandleMessageNotification(new(notification));
    }
    private SettingsWindow? _settingsWindow;
    private MessageNotificationWindow? _messageBubble;
    private MessageNotificationSummary? _displayedMessageSummary;
    private bool _readingQqDetails;
    private DateTimeOffset _lastQqDetailsRead;

    private bool TryEnrichActiveMessage(MessageNotificationSummary notification, bool sourceIsForeground, bool canShow)
    {
        notification = notification.ForDisplay(_settings.Notifications.EnableQqDetailedReminders, _settings.Notifications.EnableWeChatDetailedReminders);
        if (sourceIsForeground || !canShow || _activeMessageProvider != notification.Provider ||
            _displayedMessageSummary is null || (notification.ConversationDisplayName is null && notification.NewNotificationCount is null)) return false;
        // A richer Toast following a Shell signal updates the existing card without restarting its animation or timer.
        ShowMessageNotification(notification with
        {
            ApplicationIcon = notification.ApplicationIcon ?? _displayedMessageSummary.ApplicationIcon,
        });
        return true;
    }

    private void PositionMessageNotification()
    {
        if (_messageBubble is null || _displayedMessageSummary is null || _isClosing) return;
        UpdateLayout();
        // Ignore the repeating sway and alpha changes; neither should move the card or switch its side.
        double width = PetImage.Width, height = PetImage.Height;
        Point topLeft = PointToScreen(new Point((ActualWidth - width) / 2,
            ActualHeight - PetVisual.Margin.Bottom - height));
        Point bottomRight = PointToScreen(new Point((ActualWidth + width) / 2,
            ActualHeight - PetVisual.Margin.Bottom));
        DesktopRectangle character = new(topLeft.X, topLeft.Y,
            bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
        DesktopRectangle work = _windowWorkAreaProvider.GetForWindow(new WindowInteropHelper(this).Handle);
        Matrix scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        MessageSidePosition position = MessageSidePlacement.Resolve(character,
            _messageBubble.Width * scale.M11, _messageBubble.Height * scale.M22, work,
            4 * scale.M11, 8 * scale.M11);
        ApplyBodyReactionMirror(position.MirrorCharacter);
        _messageBubble.ShowAt(position);
    }

    private async Task RefreshQqDetailsAsync()
    {
        if (!_persistSettings || _readingQqDetails || _isClosing ||
            !_settings.Notifications.EnableQqDetailedReminders ||
            _activeMessageProvider != MessageProvider.Qq || _displayedMessageSummary is null ||
            DateTimeOffset.Now - _lastQqDetailsRead < TimeSpan.FromSeconds(2)) return;
        Guid? token = _messageNotificationReactionToken;
        _readingQqDetails = true;
        _lastQqDetailsRead = DateTimeOffset.Now;
        try
        {
            QqTrayDetails? details = await Task.Run(QqTrayDetailsReader.TryRead);
            // A late response cannot leak details after disable, replace a newer notification or reopen a hidden card.
            if (details is null || _displayedMessageSummary?.MessagePreview is not null || _displayedMessageSummary?.NotificationKey is not null || _isClosing || !_settings.Notifications.EnableQqDetailedReminders ||
                _activeMessageProvider != MessageProvider.Qq || _messageNotificationReactionToken != token ||
                _displayedMessageSummary is null) return;
            ShowMessageNotification(_displayedMessageSummary with
            {
                ConversationDisplayName = details.DisplayName,
                UnreadCount = details.UnreadCount,
            });
        }
        finally { _readingQqDetails = false; }
    }
}
