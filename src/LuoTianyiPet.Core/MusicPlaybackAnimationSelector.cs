namespace LuoTianyiPet.Core;

public sealed class MusicPlaybackAnimationSelector
{
    public static IReadOnlyList<string> Candidates { get; } =
    [
        PetVisualState.EnjoyMusicAnimation,
        PetVisualState.MusicSwayAnimation,
    ];

    private readonly Func<int, int> _selectIndex;

    public MusicPlaybackAnimationSelector(Func<int, int>? selectIndex = null)
    {
        _selectIndex = selectIndex ?? Random.Shared.Next;
    }

    public string Select()
    {
        int index = _selectIndex(Candidates.Count);
        if (index < 0 || index >= Candidates.Count)
        {
            throw new InvalidOperationException("The music animation selector returned an invalid index.");
        }

        return Candidates[index];
    }
}
