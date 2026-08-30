namespace LuoTianyiPet.Core;

public sealed class MusicPlaybackAnimationSelector
{
    public static IReadOnlyList<string> LuoTianyiPerformanceCandidates { get; } =
    [
        PetVisualState.EnjoyMusicAnimation,
        PetVisualState.MusicSwayAnimation,
    ];

    private readonly Func<int, int> _selectIndex;

    public MusicPlaybackAnimationSelector(Func<int, int>? selectIndex = null)
    {
        _selectIndex = selectIndex ?? Random.Shared.Next;
    }

    public string SelectForArtist(string? artist)
    {
        if (!MusicArtistMatcher.IsLuoTianyi(artist))
        {
            return PetVisualState.EnjoyMusicAnimation;
        }

        int index = _selectIndex(LuoTianyiPerformanceCandidates.Count);
        if (index < 0 || index >= LuoTianyiPerformanceCandidates.Count)
        {
            throw new InvalidOperationException("The music animation selector returned an invalid index.");
        }

        return LuoTianyiPerformanceCandidates[index];
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
