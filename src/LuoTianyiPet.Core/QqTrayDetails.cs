namespace LuoTianyiPet.Core;

public sealed record QqTrayDetails(string DisplayName, int? UnreadCount);
public sealed record QqTrayHeader(string DisplayName, string UnreadBadge);

public static class QqTrayDetailsParser
{
    public static QqTrayDetails? Parse(IReadOnlyList<QqTrayHeader> rows)
    {
        if (rows.Count is < 1 or > 10) return null;
        List<string> names = [];
        int total = 0;
        bool exactCount = true;
        foreach (QqTrayHeader row in rows)
        {
            string name = row.DisplayName.Trim();
            if (name.Length is < 1 or > 64 || name.Any(char.IsControl)) return null;
            if (int.TryParse(row.UnreadBadge, out int count) && count is > 0 and <= 99999)
                total += count;
            else if (row.UnreadBadge.EndsWith("+", StringComparison.Ordinal) &&
                int.TryParse(row.UnreadBadge.Substring(0, row.UnreadBadge.Length - 1), out count) && count > 0)
                exactCount = false;
            else return null;
            names.Add(name);
        }
        string label = string.Join("、", names.Take(2));
        if (names.Count > 2) label += $" 等{names.Count}个会话";
        return new(label, exactCount ? total : null);
    }
}
