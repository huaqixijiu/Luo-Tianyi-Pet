namespace LuoTianyiPet.Core;

public enum MessageProvider
{
    Qq,
    WeChat,
}

public enum MessageNotificationAccessStatus
{
    PackageIdentityRequired,
    Unspecified,
    Denied,
    Allowed,
    Unavailable,
}

public sealed record MessageNotificationSummary(
    MessageProvider Provider,
    DateTimeOffset OccurredAt,
    string? ConversationDisplayName = null,
    ReadOnlyMemory<byte>? ApplicationIcon = null,
    ReadOnlyMemory<byte>? ContactAvatar = null,
    int? UnreadCount = null,
    string? MessagePreview = null,
    int? NewNotificationCount = null,
    string? NotificationKey = null,
    string? WeChatSessionKey = null)
{
    public MessageNotificationSummary ForDisplay(bool enableQqDetails, bool enableWeChatDetails = true) =>
        (Provider == MessageProvider.Qq ? !enableQqDetails : !enableWeChatDetails)
            ? this with { ConversationDisplayName = null, ContactAvatar = null, UnreadCount = null,
                MessagePreview = null, NewNotificationCount = null }
            : this;
}

public interface IMessageNotificationDetailSettings
{
    void SetQqDetailsEnabled(bool enabled);
    void SetWeChatDetailsEnabled(bool enabled);
}

public sealed class MessageNotificationReceivedEventArgs(
    MessageNotificationSummary notification) : EventArgs
{
    public MessageNotificationSummary Notification { get; } =
        notification ?? throw new ArgumentNullException(nameof(notification));

    public MessageProvider Provider => Notification.Provider;

    public DateTimeOffset OccurredAt => Notification.OccurredAt;
}

public interface IMessageNotificationSource : IDisposable
{
    event EventHandler<MessageNotificationReceivedEventArgs>? NotificationReceived;

    MessageNotificationAccessStatus GetAccessStatus();

    ValueTask<MessageNotificationAccessStatus> RequestAccessAsync();

    void Start();

    void Stop();
}

public sealed class MessageProviderMatcher
{
    private static readonly string[] BuiltInQqProcessNames = ["qq", "qqnt"];
    private static readonly string[] BuiltInWeChatProcessNames = ["wechat", "weixin", "wechatappex"];
    private readonly string[] _qqApplicationIdentifiers;
    private readonly string[] _wechatApplicationIdentifiers;
    private readonly string[] _qqProcessNames;
    private readonly string[] _wechatProcessNames;

    public MessageProviderMatcher(MessageNotificationPreferences preferences)
    {
        Guard.NotNull(preferences, nameof(preferences));
        _qqApplicationIdentifiers = Parse(preferences.QqApplicationIdentifiers);
        _wechatApplicationIdentifiers = Parse(preferences.WeChatApplicationIdentifiers);
        _qqProcessNames = ParseProcessNames(preferences.QqProcessNames)
            .Concat(BuiltInQqProcessNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _wechatProcessNames = ParseProcessNames(preferences.WeChatProcessNames)
            .Concat(BuiltInWeChatProcessNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public MessageProvider? Identify(string? appUserModelId, string? displayName)
    {
        if (MatchesApplication(_qqApplicationIdentifiers, appUserModelId, displayName))
        {
            return MessageProvider.Qq;
        }

        return MatchesApplication(_wechatApplicationIdentifiers, appUserModelId, displayName)
            ? MessageProvider.WeChat
            : null;
    }

    public bool IsForegroundProcess(MessageProvider provider, string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        string normalized = NormalizeProcessName(processName!);
        string[] candidates = provider == MessageProvider.Qq
            ? _qqProcessNames
            : _wechatProcessNames;
        return candidates.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    public MessageProvider? IdentifyProcess(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        string normalized = NormalizeProcessName(processName!);
        if (_qqProcessNames.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return MessageProvider.Qq;
        }

        return _wechatProcessNames.Contains(normalized, StringComparer.OrdinalIgnoreCase)
            ? MessageProvider.WeChat
            : null;
    }

    public static string GetDisplayName(MessageProvider provider) => provider switch
    {
        MessageProvider.Qq => "QQ",
        MessageProvider.WeChat => "微信",
        _ => throw new ArgumentOutOfRangeException(nameof(provider)),
    };

    private static bool MatchesApplication(
        IEnumerable<string> identifiers,
        string? appUserModelId,
        string? displayName)
    {
        string normalizedDisplayName = NormalizeIdentity(displayName);
        string normalizedAppUserModelId = NormalizeIdentity(appUserModelId);
        foreach (string identifier in identifiers)
        {
            if (normalizedDisplayName == identifier ||
                (!string.IsNullOrEmpty(normalizedAppUserModelId) &&
                    normalizedAppUserModelId.IndexOf(identifier, StringComparison.Ordinal) >= 0))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] Parse(string value) => TextParsing.SplitAndTrim(value, ';')
        .Select(NormalizeIdentity)
        .Where(value => value.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private static string[] ParseProcessNames(string value) => TextParsing.SplitAndTrim(value, ';')
        .Select(NormalizeProcessName)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string NormalizeIdentity(string? value) => string.Concat(
        (value ?? string.Empty)
            .Where(character => char.IsLetterOrDigit(character)))
        .ToLowerInvariant();

    private static string NormalizeProcessName(string value) =>
        Path.GetFileNameWithoutExtension(value.Trim());
}

public enum MessageNotificationDecision
{
    Show,
    Deferred,
    IgnoredDuplicate,
    IgnoredSourceForeground,
}

public static class MessageNotificationPresentationPolicy
{
    public static TimeSpan DefaultDuration { get; } = TimeSpan.FromSeconds(30);

    public static bool HasAvailablePresentationSlot(
        PlaybackPlanSource currentSource,
        Guid? activeReactionToken,
        Guid? messageReactionToken) =>
        currentSource == PlaybackPlanSource.Continuous ||
        (messageReactionToken is Guid messageToken && activeReactionToken == messageToken);
}

public sealed class MessageNotificationCoordinator
{
    private readonly TimeSpan _duplicateWindow;
    private readonly Dictionary<MessageProvider, DateTimeOffset> _lastObserved = [];
    private readonly Dictionary<MessageProvider, MessageNotificationSummary> _pending = [];

    public MessageNotificationCoordinator(TimeSpan duplicateWindow)
    {
        if (duplicateWindow < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duplicateWindow));
        }

        _duplicateWindow = duplicateWindow;
    }

    public bool HasPending => _pending.Count > 0;

    public MessageNotificationDecision Observe(
        MessageProvider provider,
        DateTimeOffset occurredAt,
        bool sourceIsForeground,
        bool canShow) => Observe(
            new MessageNotificationSummary(provider, occurredAt),
            sourceIsForeground,
            canShow);

    public MessageNotificationDecision Observe(
        MessageNotificationSummary notification,
        bool sourceIsForeground,
        bool canShow)
    {
        Guard.NotNull(notification, nameof(notification));
        MessageProvider provider = notification.Provider;
        DateTimeOffset occurredAt = notification.OccurredAt;
        if (notification.NotificationKey is null &&
            _lastObserved.TryGetValue(provider, out DateTimeOffset lastObserved) &&
            (occurredAt <= lastObserved || occurredAt - lastObserved < _duplicateWindow))
        {
            return MessageNotificationDecision.IgnoredDuplicate;
        }

        _lastObserved[provider] = occurredAt;
        if (sourceIsForeground)
        {
            _pending.Remove(provider);
            return MessageNotificationDecision.IgnoredSourceForeground;
        }

        if (!canShow)
        {
            _pending[provider] = notification;
            return MessageNotificationDecision.Deferred;
        }

        return MessageNotificationDecision.Show;
    }

    public bool TryTakePending(
        Func<MessageProvider, bool> sourceIsForeground,
        out MessageNotificationSummary notification)
    {
        Guard.NotNull(sourceIsForeground, nameof(sourceIsForeground));
        foreach (KeyValuePair<MessageProvider, MessageNotificationSummary> pair in _pending
            .OrderBy(item => item.Value.OccurredAt)
            .ToArray())
        {
            MessageProvider candidate = pair.Key;
            MessageNotificationSummary pending = pair.Value;
            if (sourceIsForeground(candidate))
            {
                _pending.Remove(candidate);
                continue;
            }

            _pending.Remove(candidate);
            notification = pending;
            return true;
        }

        notification = null!;
        return false;
    }

    public bool TryTakePending(
        Func<MessageProvider, bool> sourceIsForeground,
        out MessageProvider provider)
    {
        bool found = TryTakePending(sourceIsForeground, out MessageNotificationSummary notification);
        provider = found ? notification.Provider : default;
        return found;
    }

    public void QueuePending(MessageNotificationSummary notification)
    {
        Guard.NotNull(notification, nameof(notification));
        _pending[notification.Provider] = notification;
    }

    public void QueuePending(MessageProvider provider, DateTimeOffset occurredAt) =>
        QueuePending(new MessageNotificationSummary(provider, occurredAt));

    public void ClearPending() => _pending.Clear();

    public void ClearPending(MessageProvider provider) => _pending.Remove(provider);

    public void DiscardPending(Func<MessageNotificationSummary, bool> shouldDiscard)
    {
        foreach (var pair in _pending.Where(pair => shouldDiscard(pair.Value)).ToArray())
            _pending.Remove(pair.Key);
    }
}

public sealed class ShellAttentionSessionTracker
{
    private readonly TimeSpan _continuationWindow;
    private readonly Dictionary<long, DateTimeOffset> _lastSignals = [];

    public ShellAttentionSessionTracker(TimeSpan continuationWindow)
    {
        if (continuationWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(continuationWindow));
        }

        _continuationWindow = continuationWindow;
    }

    public bool ShouldNotify(long sourceKey, DateTimeOffset occurredAt)
    {
        if (sourceKey == 0)
        {
            return false;
        }

        if (_lastSignals.TryGetValue(sourceKey, out DateTimeOffset previous) &&
            occurredAt >= previous && occurredAt - previous <= _continuationWindow)
        {
            _lastSignals[sourceKey] = occurredAt;
            return false;
        }

        _lastSignals[sourceKey] = occurredAt;
        foreach (long expired in _lastSignals
            .Where(pair => occurredAt - pair.Value > _continuationWindow)
            .Select(pair => pair.Key)
            .ToArray())
        {
            if (expired != sourceKey)
            {
                _lastSignals.Remove(expired);
            }
        }

        return true;
    }

    public void Reset() => _lastSignals.Clear();
}
