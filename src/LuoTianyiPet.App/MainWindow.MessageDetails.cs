using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private readonly MessageNotificationCounter _messageNotificationCounter = new();
    private SettingsWindow? _settingsWindow;
    private MessageNotificationWindow? _messageBubble;
    private MessageNotificationSummary? _displayedMessageSummary;
    private bool _readingQqDetails;
    private DateTimeOffset _lastQqDetailsRead;

    private bool TryEnrichActiveMessage(MessageNotificationSummary notification, bool sourceIsForeground, bool canShow)
    {
        notification = notification.ForDisplay(_settings.Notifications.EnableQqDetailedReminders);
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
