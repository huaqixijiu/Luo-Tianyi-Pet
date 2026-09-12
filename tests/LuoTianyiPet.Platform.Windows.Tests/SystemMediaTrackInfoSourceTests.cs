using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.Platform.Windows.Tests;

public sealed class SystemMediaTrackInfoSourceTests
{
    [Theory]
    [InlineData(0, PetVisualState.MusicSwayAnimation)]
    [InlineData(1, PetVisualState.OneClickSingingAnimation)]
    public void IncompleteSystemArtist_UsesSameTrackCollaboratorsForBothSingingAnimations(
        int randomIndex, string expectedAnimation)
    {
        SystemMediaTrackInfoSource.TryParseWindowTitle(
            "测试合作曲 - 测试歌手/洛天依Official", out string title, out string artist);
        MediaTrackSnapshot snapshot = SystemMediaTrackInfoSource.SupplementArtistFromWindow(
            new(true, true, title, "测试歌手"), new(true, true, title, artist));

        Assert.Equal(artist, snapshot.Artist);
        Assert.Equal(expectedAnimation, new MusicPlaybackAnimationSelector(_ => randomIndex)
            .Select(MusicAnimationOptions.AutomaticSelection, snapshot.Artist));
    }

    [Theory]
    [InlineData("上一首", "测试歌手/洛天依Official")]
    [InlineData("当前歌曲", "另一位歌手/洛天依Official")]
    [InlineData("当前歌曲", "")]
    public void ConflictingOrMissingWindowMetadata_PreservesSystemMetadata(string title, string artist)
    {
        MediaTrackSnapshot media = new(true, true, "当前歌曲", "测试歌手");
        Assert.Equal(media, SystemMediaTrackInfoSource.SupplementArtistFromWindow(
            media, new(true, true, title, artist)));
    }

    [Fact]
    public void UnavailableWindow_PreservesSystemMetadata()
    {
        MediaTrackSnapshot media = new(true, true, "当前歌曲", "测试歌手");
        Assert.Equal(media, SystemMediaTrackInfoSource.SupplementArtistFromWindow(
            media, MediaTrackSnapshot.Unavailable));
    }

    [Fact]
    public void MissingSystemArtist_UsesSameTrackWindowArtist()
    {
        MediaTrackSnapshot media = new(true, true, "当前歌曲", "");
        MediaTrackSnapshot window = media with { Artist = "洛天依Official" };
        Assert.Equal(window, SystemMediaTrackInfoSource.SupplementArtistFromWindow(media, window));
    }

    [Fact]
    public void MissingSystemTrack_FallsBackToWindow()
    {
        MediaTrackSnapshot window = new(true, true, "当前歌曲", "测试歌手");
        Assert.Equal(window, SystemMediaTrackInfoSource.SupplementArtistFromWindow(
            MediaTrackSnapshot.NoSession, window));
    }

    [Theory]
    [InlineData("cloudmusic.exe", "cloudmusic.exe")]
    [InlineData("cloudmusic", "cloudmusic.exe")]
    [InlineData("com.netease.cloudmusic", "cloudmusic.exe")]
    [InlineData("C:\\Apps\\cloudmusic.exe", "cloudmusic.exe")]
    public void MatchesSourceApplication_AcceptsCloudMusicSessionIdentifiers(
        string sourceAppUserModelId,
        string targetProcessName)
    {
        Assert.True(SystemMediaTrackInfoSource.MatchesSourceApplication(
            sourceAppUserModelId,
            targetProcessName));
    }

    [Theory]
    [InlineData("msedge.exe")]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify")]
    [InlineData("notcloudmusicplayer.exe")]
    [InlineData("")]
    public void MatchesSourceApplication_RejectsOtherMediaSessions(string sourceAppUserModelId)
    {
        Assert.False(SystemMediaTrackInfoSource.MatchesSourceApplication(
            sourceAppUserModelId,
            "cloudmusic.exe"));
    }

    [Theory]
    [InlineData("奔向你 (Live) - 张睿", "奔向你 (Live)", "张睿")]
    [InlineData("达拉崩吧 - 洛天依 - 网易云音乐", "达拉崩吧", "洛天依")]
    [InlineData("单曲标题", "单曲标题", "")]
    public void TryParseWindowTitle_ExtractsTrackAndArtist(
        string windowTitle,
        string expectedTitle,
        string expectedArtist)
    {
        bool parsed = SystemMediaTrackInfoSource.TryParseWindowTitle(
            windowTitle,
            out string title,
            out string artist);

        Assert.True(parsed);
        Assert.Equal(expectedTitle, title);
        Assert.Equal(expectedArtist, artist);
    }

    [Theory]
    [InlineData("")]
    [InlineData("网易云音乐")]
    [InlineData("CloudMusic")]
    public void TryParseWindowTitle_RejectsEmptyAndGenericApplicationTitles(string windowTitle)
    {
        Assert.False(SystemMediaTrackInfoSource.TryParseWindowTitle(
            windowTitle,
            out _,
            out _));
    }
}
