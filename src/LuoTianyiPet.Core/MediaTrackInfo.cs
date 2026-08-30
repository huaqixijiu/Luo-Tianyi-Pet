namespace LuoTianyiPet.Core;

public readonly record struct MediaTrackSnapshot(
    bool ProbeSucceeded,
    bool SessionFound,
    string Title,
    string Artist)
{
    public static MediaTrackSnapshot Unavailable => new(false, false, string.Empty, string.Empty);

    public static MediaTrackSnapshot NoSession => new(true, false, string.Empty, string.Empty);

    public bool HasTrack => SessionFound && !string.IsNullOrWhiteSpace(Title);
}

public interface IMediaTrackInfoSource
{
    ValueTask<MediaTrackSnapshot> ReadAsync(string targetProcessName);
}

public static class MediaTrackText
{
    public const int MaximumTitleLength = 80;
    public const int MaximumArtistLength = 60;

    public static MediaTrackSnapshot Normalize(MediaTrackSnapshot snapshot) => snapshot with
    {
        Title = NormalizeField(snapshot.Title, MaximumTitleLength),
        Artist = NormalizeField(snapshot.Artist, MaximumArtistLength),
    };

    public static string BuildAccessibleLabel(MediaTrackSnapshot snapshot)
    {
        MediaTrackSnapshot normalized = Normalize(snapshot);
        return string.IsNullOrWhiteSpace(normalized.Artist)
            ? normalized.Title
            : $"{normalized.Title}，{normalized.Artist}";
    }

    private static string NormalizeField(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = string.Join(
            ' ',
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength];
    }
}
