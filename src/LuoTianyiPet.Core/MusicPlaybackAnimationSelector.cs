namespace LuoTianyiPet.Core;

public sealed record MusicAnimationOption(
    string SelectionId,
    string DisplayName,
    string AnimationId);

public static class MusicAnimationOptions
{
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
    ];

    public static string NormalizeSelection(string? selection) =>
        selection == RandomSelection ||
        FixedOptions.Any(option => option.SelectionId == selection)
            ? selection!
            : RandomSelection;

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

    public string Select(string? selection)
    {
        string normalized = MusicAnimationOptions.NormalizeSelection(selection);
        if (normalized != MusicAnimationOptions.RandomSelection)
        {
            return MusicAnimationOptions.ResolveFixed(normalized).AnimationId;
        }

        int index = _selectIndex(MusicAnimationOptions.FixedOptions.Count);
        if (index < 0 || index >= MusicAnimationOptions.FixedOptions.Count)
        {
            throw new InvalidOperationException("The music animation selector returned an invalid index.");
        }

        return MusicAnimationOptions.FixedOptions[index].AnimationId;
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
