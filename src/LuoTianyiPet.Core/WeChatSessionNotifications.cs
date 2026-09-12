using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace LuoTianyiPet.Core;

public sealed record WeChatSessionRow(string Key, string DisplayName, string? Preview,
    bool HasUnreadMarker, int? UnreadCount, bool Muted);

public static class WeChatSessionParser
{
    private const string Prefix = "session_item_";
    public static WeChatSessionRow? Parse(string id, string value)
    {
        if (!id.StartsWith(Prefix, StringComparison.Ordinal) || value.Length > 8192) return null;
        string[] lines = value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).ToArray();
        if (lines.Length < 3 || lines[0] != id.Substring(Prefix.Length)) return null;
        bool muted = lines[lines.Length - 1] == "消息免打扰";
        int end = lines.Length - (muted ? 1 : 0) - 1;
        // A known trailing time/status is required before accepting any preview text.
        if (end < 2 || !Regex.IsMatch(lines[end], @"^(\d{1,2}:\d{2}|(上午|下午|晚上)\s*\d{1,2}:\d{2}|昨天(\s+\d{1,2}:\d{2})?|星期[一二三四五六日天]|刚刚|\d+分钟前|\d{1,4}[/-]\d{1,2}([/-]\d{1,2})?|\d{1,2}月\d{1,2}日)$")) return null;
        Match badge = Regex.Match(lines[1], @"^\[(\d{1,6})(\+?)条\]\s*");
        int? unread = badge.Success && badge.Groups[2].Value.Length == 0 &&
            int.TryParse(badge.Groups[1].Value, out int parsed) && parsed > 0 ? parsed : null;
        string preview = string.Join(" ", new[] { badge.Success ? lines[1].Substring(badge.Length) : lines[1] }
            .Concat(lines.Skip(2).Take(end - 2)));
        preview = Clean(preview, 120);
        return new WeChatSessionRow(Hash(id), Clean(lines[0], 64), preview.Length == 0 ? null : preview,
            badge.Success, unread, muted);
    }
    public static string Hash(string text)
    {
        using var hash = SHA256.Create();
        return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text)));
    }
    private static string Clean(string value, int limit)
    {
        string text = Regex.Replace(value, @"\s+", " ").Trim();
        text = string.Concat(text.Where(c => !char.IsControl(c) &&
            CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.Format));
        if (text.Length <= limit) return text;
        int length = char.IsHighSurrogate(text[limit-2]) ? limit-2 : limit-1;
        return text.Substring(0,length) + "…";
    }
}

public sealed class WeChatSessionChangeTracker
{
    private sealed record State(bool Unread, int? Count, bool Muted, string Fingerprint);
    private Dictionary<string, State> _previous = [];
    private string _epoch = Guid.NewGuid().ToString("N");
    private long _sequence;
    public IReadOnlyList<MessageNotificationSummary> Observe(IEnumerable<WeChatSessionRow> rows,
        bool sourceIsForeground, DateTimeOffset now)
    {
        List<MessageNotificationSummary> events = [];
        Dictionary<string, State> next = [];
        // Duplicate public names cannot safely identify a conversation; ignore ambiguous rows.
        foreach (var group in rows.GroupBy(row => row.Key).Where(group => group.Count() == 1).Take(50))
        {
            var row = group.First();
            var state = new State(row.HasUnreadMarker, row.UnreadCount, row.Muted,
                WeChatSessionParser.Hash(row.Preview ?? string.Empty));
            next[row.Key] = state;
            if (!_previous.TryGetValue(row.Key, out var previous) || sourceIsForeground || row.Muted || previous.Muted ||
                !row.HasUnreadMarker) continue;
            bool decreased = state.Count is int current && previous.Count is int old && current < old;
            bool increased = state.Count is int count && previous.Count is int before && count > before;
            if (!decreased && (!previous.Unread || increased || state.Fingerprint != previous.Fingerprint))
                events.Add(new MessageNotificationSummary(MessageProvider.WeChat, now, row.DisplayName,
                    MessagePreview: row.Preview, NotificationKey: $"wechat-session:{_epoch}:{++_sequence}"));
        }
        // Keep only hashes/numbers for currently exposed rows; raw text lives only in emitted reminders.
        _previous = next;
        return events;
    }
    public void Reset() { _previous.Clear(); _epoch=Guid.NewGuid().ToString("N"); _sequence=0; }
}
