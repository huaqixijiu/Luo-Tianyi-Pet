namespace LuoTianyiPet.Core;

/// <summary>Counts captured Toast events since the source was last foregrounded. Never an unread total.</summary>
public sealed class MessageNotificationCounter
{
    private readonly Dictionary<MessageProvider, int> _counts = [];
    private readonly HashSet<string> _seen = [];
    private readonly Queue<string> _seenOrder = [];

    public bool TryObserve(MessageNotificationSummary notification, bool sourceIsForeground,
        bool enableQqDetails, out MessageNotificationSummary result, bool enableWeChatDetails = true)
    {
        result = notification.ForDisplay(enableQqDetails, enableWeChatDetails);
        if (sourceIsForeground) { Reset(notification.Provider); return true; }
        if ((notification.Provider == MessageProvider.Qq ? !enableQqDetails : !enableWeChatDetails) || notification.NotificationKey is null)
            return true; // Shell flashing identifies attention, not individual messages.
        string key = notification.Provider + ":" + notification.NotificationKey;
        if (!_seen.Add(key)) return false;
        _seenOrder.Enqueue(key);
        if (_seenOrder.Count > 2048) _seen.Remove(_seenOrder.Dequeue());
        _counts.TryGetValue(notification.Provider, out int count);
        count = count < int.MaxValue ? count + 1 : count;
        _counts[notification.Provider] = count;
        result = result with { NewNotificationCount = count };
        return true;
    }

    public void Reset(MessageProvider provider) => _counts.Remove(provider);
    public void Clear() { _counts.Clear(); _seen.Clear(); _seenOrder.Clear(); }
}
