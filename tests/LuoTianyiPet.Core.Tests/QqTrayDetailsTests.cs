using System.Text.Json;
using Xunit;
namespace LuoTianyiPet.Core.Tests;

public class QqTrayDetailsTests
{
    [Fact]
    public void AggregatesOnlyExplicitUnreadBadges()
    {
        var result = QqTrayDetailsParser.Parse([new("测试好友", "5"), new("测试群", "1")]);
        Assert.Equal("测试好友、测试群",result!.DisplayName);
        Assert.Equal(6,result.UnreadCount);
    }
    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("消息预览")]
    public void AmbiguousCountFailsClosed(string count) =>
        Assert.Null(QqTrayDetailsParser.Parse([new("测试好友",count)]));
    [Fact]
    public void CappedBadgeDoesNotPretendToBeExact() =>
        Assert.Null(QqTrayDetailsParser.Parse([new("测试好友","99+")])!.UnreadCount);
    [Fact]
    public void DisablingQqDetailsRemovesAllSensitiveDisplayFields()
    {
        var source = new MessageNotificationSummary(MessageProvider.Qq,DateTimeOffset.Now,
            "测试好友",ContactAvatar: new byte[]{1,2},UnreadCount: 5);
        var result = source.ForDisplay(false);
        Assert.Null(result.ConversationDisplayName);
        Assert.Null(result.UnreadCount);
        Assert.Null(result.ContactAvatar);
        Assert.Equal(MessageProvider.Qq,result.Provider);
    }
    [Fact]
    public void QqSwitchDoesNotStripWeChatToastTitle()
    {
        var source = new MessageNotificationSummary(MessageProvider.WeChat,DateTimeOffset.Now,"测试群");
        Assert.Equal(source,source.ForDisplay(false));
    }
    [Fact]
    public void ExistingSettingsDefaultOnAndFalseRoundTrips()
    {
        Assert.True(JsonSerializer.Deserialize<MessageNotificationPreferences>("{}")!.EnableQqDetailedReminders);
        var setting = new MessageNotificationPreferences { EnableQqDetailedReminders = false };
        Assert.False(JsonSerializer.Deserialize<MessageNotificationPreferences>(JsonSerializer.Serialize(setting))!.EnableQqDetailedReminders);
    }
}
