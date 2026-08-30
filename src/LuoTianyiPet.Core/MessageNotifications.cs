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

public sealed class MessageNotificationReceivedEventArgs(
    MessageProvider provider,
    DateTimeOffset occurredAt) : EventArgs
{
    public MessageProvider Provider { get; } = provider;

    public DateTimeOffset OccurredAt { get; } = occurredAt;
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
    private readonly string[] _qqApplicationIdentifiers;
    private readonly string[] _wechatApplicationIdentifiers;
    private readonly string[] _qqProcessNames;
    private readonly string[] _wechatProcessNames;

    public MessageProviderMatcher(MessageNotificationPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        _qqApplicationIdentifiers = Parse(preferences.QqApplicationIdentifiers);
        _wechatApplicationIdentifiers = Parse(preferences.WeChatApplicationIdentifiers);
        _qqProcessNames = ParseProcessNames(preferences.QqProcessNames);
        _wechatProcessNames = ParseProcessNames(preferences.WeChatProcessNames);
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

        string normalized = NormalizeProcessName(processName);
        string[] candidates = provider == MessageProvider.Qq
            ? _qqProcessNames
            : _wechatProcessNames;
        return candidates.Contains(normalized, StringComparer.OrdinalIgnoreCase);
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
                    normalizedAppUserModelId.Contains(identifier, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] Parse(string value) => value
        .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Select(NormalizeIdentity)
        .Where(value => value.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private static string[] ParseProcessNames(string value) => value
        .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
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

public sealed class MessageNotificationCoordinator
{
    private readonly TimeSpan _duplicateWindow;
    private readonly Dictionary<MessageProvider, DateTimeOffset> _lastObserved = [];
    private readonly Dictionary<MessageProvider, DateTimeOffset> _pending = [];

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
        bool canShow)
    {
        if (_lastObserved.TryGetValue(provider, out DateTimeOffset lastObserved) &&
            occurredAt >= lastObserved &&
            occurredAt - lastObserved < _duplicateWindow)
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
            _pending[provider] = occurredAt;
            return MessageNotificationDecision.Deferred;
        }

        return MessageNotificationDecision.Show;
    }

    public bool TryTakePending(
        Func<MessageProvider, bool> sourceIsForeground,
        out MessageProvider provider)
    {
        ArgumentNullException.ThrowIfNull(sourceIsForeground);
        foreach ((MessageProvider candidate, _) in _pending.OrderBy(pair => pair.Value).ToArray())
        {
            if (sourceIsForeground(candidate))
            {
                _pending.Remove(candidate);
                continue;
            }

            _pending.Remove(candidate);
            provider = candidate;
            return true;
        }

        provider = default;
        return false;
    }

    public void QueuePending(MessageProvider provider, DateTimeOffset occurredAt) =>
        _pending[provider] = occurredAt;

    public void ClearPending() => _pending.Clear();
}
