using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class MediaTrackTextTests
{
    [Fact]
    public void Normalize_CollapsesWhitespaceAndKeepsTitleAndArtistInMemorySafeBounds()
    {
        MediaTrackSnapshot snapshot = new(
            true,
            true,
            $"  星   光\r\n{new string('曲', 90)}  ",
            $"  洛天依\t{new string('歌', 70)}  ");

        MediaTrackSnapshot normalized = MediaTrackText.Normalize(snapshot);

        Assert.StartsWith("星 光 曲", normalized.Title, StringComparison.Ordinal);
        Assert.Equal(MediaTrackText.MaximumTitleLength, normalized.Title.Length);
        Assert.StartsWith("洛天依 歌", normalized.Artist, StringComparison.Ordinal);
        Assert.Equal(MediaTrackText.MaximumArtistLength, normalized.Artist.Length);
    }

    [Fact]
    public void BuildAccessibleLabel_IncludesArtistOnlyWhenAvailable()
    {
        MediaTrackSnapshot withArtist = new(true, true, "达拉崩吧", "洛天依");
        MediaTrackSnapshot withoutArtist = withArtist with { Artist = " " };

        Assert.Equal("达拉崩吧，洛天依", MediaTrackText.BuildAccessibleLabel(withArtist));
        Assert.Equal("达拉崩吧", MediaTrackText.BuildAccessibleLabel(withoutArtist));
    }
}
