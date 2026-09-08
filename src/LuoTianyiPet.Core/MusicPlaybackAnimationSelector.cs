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
        _selectIndex = selectIndex ?? Random.Shared.Next;
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

        bool isLuoTianyi = MusicArtistMatcher.IsLuoTianyi(artist);
        if (!isLuoTianyi)
        {
            return PetVisualState.EnjoyMusicAnimation;
        }

        if (normalized != MusicAnimationOptions.AutomaticSelection)
        {
            string fixedAnimation = MusicAnimationOptions.ResolveFixed(normalized).AnimationId;
            return fixedAnimation == PetVisualState.OneClickSingingAnimation &&
                !enableLuoTianyiSingingEasterEgg
                    ? PetVisualState.MusicSwayAnimation
                    : fixedAnimation;
        }

        IReadOnlyList<string> easterEggPool = enableLuoTianyiSingingEasterEgg
            ? [PetVisualState.MusicSwayAnimation, PetVisualState.OneClickSingingAnimation]
            : [PetVisualState.MusicSwayAnimation];
        int index = _selectIndex(easterEggPool.Count);
        if (index < 0 || index >= easterEggPool.Count)
        {
            throw new InvalidOperationException("The music animation selector returned an invalid index.");
        }

        return easterEggPool[index];
    }
}

public static class MusicArtistMatcher
{
    public static bool IsLuoTianyi(string? artist)
    {
        if (string.IsNullOrWhiteSpace(artist))
        {
            return false;
        }

        char[] separators = ['/', '\\', '、', ',', '，', '&', '+', '＋', ';', '；'];
        return artist
            .Split(separators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(token => new string(
                token
                    .Where(character =>
                        !char.IsWhiteSpace(character) && character is not '-' and not '_')
                    .Select(char.ToLowerInvariant)
                    .ToArray()))
            .Any(token =>
                token.Equals("洛天依", StringComparison.Ordinal) ||
                token.Equals("luotianyi", StringComparison.Ordinal));
    }
}
