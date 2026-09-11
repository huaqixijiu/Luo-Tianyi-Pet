namespace LuoTianyiPet.Core;

public sealed record MusicAnimationOption(
    string SelectionId,
    string DisplayName,
    string AnimationId);

public static class MusicAnimationOptions
{
    public const string AutomaticSelection = "automatic-by-artist";
    public const string NoneSelection = PetVisualState.NoMusicAnimation;
    // Retained only to migrate settings written before singer-aware selection existed.
    public const string RandomSelection = "random";

    public static IReadOnlyList<MusicAnimationOption> FixedOptions { get; } =
    [
        new(
            PetVisualState.EnjoyMusicAnimation,
            "心律共鸣 · 享受音乐",
            PetVisualState.EnjoyMusicAnimation),
        new(
            PetVisualState.MusicSwayAnimation,
            "九周年 · 音乐摇摆",
            PetVisualState.MusicSwayAnimation),
        new(
            PetVisualState.OneClickSingingAnimation,
            "元旦祝福 · 一键唱歌",
            PetVisualState.OneClickSingingAnimation),
    ];

    public static string NormalizeSelection(string? selection) => selection switch
    {
        AutomaticSelection or NoneSelection => selection,
        RandomSelection => AutomaticSelection,
        _ when FixedOptions.Any(option => option.SelectionId == selection) => selection!,
        _ => AutomaticSelection,
    };

    public static MusicAnimationOption ResolveFixed(string selection) =>
        FixedOptions.First(option => option.SelectionId == selection);
}

public sealed class MusicPlaybackAnimationSelector
{
    private readonly Func<int, int> _selectIndex;

    public MusicPlaybackAnimationSelector(Func<int, int>? selectIndex = null)
    {
        _selectIndex = selectIndex ?? SharedRandom.Next;
    }

    public string Select(
        string? selection,
        string? artist,
        bool enableLuoTianyiSingingEasterEgg = true)
    {
        string normalized = MusicAnimationOptions.NormalizeSelection(selection);
        if (normalized == MusicAnimationOptions.NoneSelection)
        {
            return PetVisualState.NoMusicAnimation;
        }

        if (normalized != MusicAnimationOptions.AutomaticSelection)
        {
            // A fixed choice is deliberately artist-independent: users selecting one
            // preview card expect that to be the only music animation they see.
            return MusicAnimationOptions.ResolveFixed(normalized).AnimationId;
        }

        // 洛天依优先于其它合唱歌手，避免“洛天依 / 乐正绫”被分到打 call。
        if (MusicArtistMatcher.IsLuoTianyi(artist))
        {
            IReadOnlyList<string> easterEggPool =
                [PetVisualState.MusicSwayAnimation, PetVisualState.OneClickSingingAnimation];
            int index = _selectIndex(easterEggPool.Count);
            if (index < 0 || index >= easterEggPool.Count)
            {
                throw new InvalidOperationException("The music animation selector returned an invalid index.");
            }

            return easterEggPool[index];
        }

        if (MusicArtistMatcher.IsYuezhengLing(artist))
        {
            return PetVisualState.YuezhengLingCallAnimation;
        }

        return PetVisualState.EnjoyMusicAnimation;
    }
}

public static class MusicArtistMatcher
{
    public static bool IsLuoTianyi(string? artist) =>
        ContainsArtist(artist, "洛天依", "luotianyi");

    public static bool IsYuezhengLing(string? artist) =>
        ContainsArtist(artist, "乐正绫", "yuezhengling");

    private static bool ContainsArtist(string? artist, string chineseName, string englishName)
    {
        if (string.IsNullOrWhiteSpace(artist))
        {
            return false;
        }

        string separated = artist!;
        string[] collaborationMarkers = ["featuring", "feat.", "feat", "with", "vs.", "vs"];
        foreach (string marker in collaborationMarkers)
        {
            separated = ReplaceOrdinalIgnoreCase(separated, marker, "|");
        }

        char[] separators =
            ['/', '\\', '、', ',', '，', '&', '+', '＋', ';', '；', '|', '｜', '×', '·', '•'];
        bool tokenMatch = TextParsing.SplitAndTrim(separated, separators)
            .Select(CompactArtistToken)
            .Any(token =>
                token.Equals(chineseName, StringComparison.Ordinal) ||
                token.StartsWith(chineseName + "official", StringComparison.Ordinal) ||
                token.Equals(englishName, StringComparison.Ordinal) ||
                token.StartsWith(englishName + "official", StringComparison.Ordinal));
        if (tokenMatch)
        {
            return true;
        }

        string compactArtist = CompactArtistToken(artist!);
        return ContainsQualifiedName(compactArtist, chineseName) ||
            ContainsQualifiedName(compactArtist, englishName);
    }

    private static string CompactArtistToken(string token) => new(
        token
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static bool ContainsQualifiedName(string compactArtist, string name)
    {
        int searchIndex = 0;
        while (searchIndex < compactArtist.Length)
        {
            int matchIndex = compactArtist.IndexOf(
                name,
                searchIndex,
                StringComparison.Ordinal);
            if (matchIndex < 0)
            {
                return false;
            }

            string suffix = compactArtist.Substring(matchIndex + name.Length);
            if (suffix.Length == 0 || suffix.StartsWith("official", StringComparison.Ordinal))
            {
                return true;
            }

            searchIndex = matchIndex + name.Length;
        }

        return false;
    }

    private static string ReplaceOrdinalIgnoreCase(
        string source,
        string oldValue,
        string newValue)
    {
        int startIndex = 0;
        while (true)
        {
            int matchIndex = source.IndexOf(
                oldValue,
                startIndex,
                StringComparison.OrdinalIgnoreCase);
            if (matchIndex < 0)
            {
                return source;
            }

            source = source.Substring(0, matchIndex) +
                newValue +
                source.Substring(matchIndex + oldValue.Length);
            startIndex = matchIndex + newValue.Length;
        }
    }
}
