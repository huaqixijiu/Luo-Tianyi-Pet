namespace LuoTianyiPet.Core;

public sealed record InboxConversation(string Key, MessageProvider Provider, string Name, string Preview,
    int Count, DateTimeOffset UpdatedAt, MessageNotificationSummary Latest);

/// <summary>Session-local reminders, never a replica of the client's unread database.</summary>
public sealed class MessageInbox
{
    private readonly Dictionary<string, InboxConversation> _rows = [];
    private readonly HashSet<string> _seen = [];
    private readonly Dictionary<MessageProvider, DateTimeOffset> _dismissed = [];
    public IReadOnlyList<InboxConversation> Rows(MessageProvider provider) => _rows.Values
        .Where(row => row.Provider == provider).OrderByDescending(row => row.UpdatedAt).ThenBy(row => row.Key).ToArray();
    public long Total(MessageProvider provider) => _rows.Values.Where(row => row.Provider == provider).Sum(row => (long)row.Count);
    public static string Badge(long count) => count > 99 ? "99+" : count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static string GenericKey(MessageProvider provider) => $"{provider}:source";

    public bool Add(MessageNotificationSummary message)
    {
        message = message with {ApplicationIcon = null, ContactAvatar = null};
        string? eventKey = message.NotificationKey is string id ? $"{message.Provider}:{id}" : null;
        if (eventKey is not null && !_seen.Add(eventKey)) return false;
        if (_dismissed.TryGetValue(message.Provider, out var dismissed) && message.OccurredAt <= dismissed) return false;
        bool named = !string.IsNullOrWhiteSpace(message.ConversationDisplayName);
        string generic = GenericKey(message.Provider);
        // Shell is an uncounted source hint; repeated flashes must not inflate existing details.
        if (eventKey is null && Total(message.Provider) > 0) return false;
        string key = named ? $"{message.Provider}:" + WeChatSessionParser.Hash(message.ConversationDisplayName!) : generic;
        int transferred = 0;
        if (named && _rows.TryGetValue(generic, out var hint) && hint.Count == 1 && hint.Latest.NotificationKey is null &&
            Math.Abs((message.OccurredAt - hint.UpdatedAt).TotalSeconds) <= 3)
        {
            transferred = hint.Count;
            _rows.Remove(generic);
        }
        _rows.TryGetValue(key, out var previous);
        bool crossSourceDuplicate = previous is not null && named && message.Provider == MessageProvider.WeChat &&
            (previous.Latest.WeChatSessionKey is null) != (message.WeChatSessionKey is null) &&
            previous.Preview == (message.MessagePreview ?? "有新消息") &&
            Math.Abs((message.OccurredAt - previous.UpdatedAt).TotalSeconds) <= 2;
        int count = previous?.Count ?? 0;
        if (!crossSourceDuplicate) count = (int)Math.Min(int.MaxValue, (long)count + Math.Max(1, transferred));
        if (previous is not null && message.OccurredAt < previous.UpdatedAt)
        { _rows[key] = previous with {Count=count}; return true; }
        _rows[key] = new(key, message.Provider, named ? message.ConversationDisplayName! :
            MessageProviderMatcher.GetDisplayName(message.Provider) + "提醒", message.MessagePreview ?? "有新消息",
            count, message.OccurredAt, message);
        return true;
    }

    public void Ignore(string key) => _rows.Remove(key);
    public void Clear(MessageProvider provider, DateTimeOffset now)
    {
        foreach (string key in _rows.Where(pair => pair.Value.Provider == provider).Select(pair => pair.Key).ToArray()) _rows.Remove(key);
        _dismissed[provider] = now;
    }
    public void Prune(Func<MessageNotificationSummary, bool> invalid)
    {
        foreach (var row in _rows.Values.Where(row => invalid(row.Latest)).ToArray()) _rows.Remove(row.Key);
    }
    public void HideDetails(MessageProvider provider)
    {
        var rows = Rows(provider);
        if (rows.Count == 0) return;
        int total = (int)Math.Min(int.MaxValue, Total(provider));
        foreach (var row in rows) _rows.Remove(row.Key);
        var latest = rows[0].Latest with { ConversationDisplayName = null, MessagePreview = null,
            ContactAvatar = null, WeChatSessionKey = null, UnreadCount = null, NewNotificationCount = null };
        _rows[GenericKey(provider)] = new(GenericKey(provider), provider,
            MessageProviderMatcher.GetDisplayName(provider) + "提醒", "有新消息", total, latest.OccurredAt, latest);
    }
}

public sealed class MessageInboxInteraction
{
    public MessageProvider? OpenProvider { get; private set; }
    public bool Pinned { get; private set; }
    private MessageProvider? _hover;
    private DateTimeOffset _entered, _left;
    private bool _outside, _suppressHover;
    public void Move(MessageProvider? icon, bool inPanelOrBridge, DateTimeOffset now)
    {
        if (icon != _hover) { _hover = icon; _entered = now; _suppressHover = false; }
        bool outside = icon is null && !inPanelOrBridge;
        if (outside && !_outside) _left = now;
        _outside = outside;
        if (!_suppressHover && icon is MessageProvider provider && now - _entered >= TimeSpan.FromMilliseconds(300))
            OpenProvider = provider;
        if (outside && !Pinned && now - _left >= TimeSpan.FromMilliseconds(500)) Close();
    }
    public void Click(MessageProvider provider)
    {
        if (OpenProvider == provider && Pinned) { Close(); _hover = provider; _suppressHover = true; }
        else { OpenProvider = provider; Pinned = true; }
    }
    public void Close() { OpenProvider = null; Pinned = false; }
    public void OutsideClick() { Close(); _suppressHover = true; }
}
