using LuoTianyiPet.Core;

namespace LuoTianyiPet.Platform.Windows.Tests;

public sealed class WindowsMessageNotificationSourceTests
{
    [Theory]
    [InlineData(2, "郁离", "郁离")]
    [InlineData(3, " 天依应援群 ", "天依应援群")]
    [InlineData(2, "郁\r\n离", "郁离")]
    public void ConversationTitleUsesOnlyFirstElementWhenBodyIsSeparate(
        int elementCount,
        string firstText,
        string expected)
    {
        Assert.Equal(
            expected,
            NotificationConversationTitleSelector.Select(elementCount, firstText));
    }

    [Theory]
    [InlineData(0, "郁离")]
    [InlineData(1, "这可能是消息正文")]
    [InlineData(2, "  ")]
    public void ConversationTitleFailsClosedWhenStructureIsAmbiguous(
        int elementCount,
        string firstText)
    {
        Assert.Null(NotificationConversationTitleSelector.Select(elementCount, firstText));
    }

    [Fact]
    public void ConversationTitleIsLengthLimited()
    {
        string result = Assert.IsType<string>(
            NotificationConversationTitleSelector.Select(2, new string('天', 80)));

        Assert.Equal(64, result.Length);
        Assert.EndsWith("…", result);
    }

    [Fact]
    public async Task UnpackagedTestHostFailsClosedWithoutRequestingPermission()
    {
        MessageProviderMatcher matcher = new(new MessageNotificationPreferences());
        using WindowsMessageNotificationSource source = new(matcher);

        Assert.False(WindowsPackageIdentity.HasCurrentPackageIdentity());
        Assert.Equal(
            MessageNotificationAccessStatus.PackageIdentityRequired,
            source.GetAccessStatus());
        Assert.Equal(
            MessageNotificationAccessStatus.PackageIdentityRequired,
            await source.RequestAccessAsync());

        source.Start();
        source.Stop();
    }
}
