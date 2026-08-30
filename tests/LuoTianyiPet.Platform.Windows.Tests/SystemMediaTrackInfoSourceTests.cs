using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.Platform.Windows.Tests;

public sealed class SystemMediaTrackInfoSourceTests
{
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
