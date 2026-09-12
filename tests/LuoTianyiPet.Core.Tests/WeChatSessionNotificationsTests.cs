using LuoTianyiPet.Core;
using System.Text.Json;
namespace LuoTianyiPet.Core.Tests;
public sealed class WeChatSessionNotificationsTests
{
    private static WeChatSessionRow Row(string content="你好", int count=1, bool muted=false) =>
        WeChatSessionParser.Parse("session_item_测试好友",$"测试好友\n[{count}条] {content}\n20:45"+(muted?"\n消息免打扰":""))!;
    [Fact] public void ParsesTheObservedPrefixAndSeparateLineForms()
    {
        var row=Row();Assert.Equal("测试好友",row.DisplayName);Assert.Equal("你好",row.Preview);Assert.Equal(1,row.UnreadCount);
        var separate=WeChatSessionParser.Parse("session_item_测试好友","测试好友\n[2条]\n多行\n预览\n20:45")!;
        Assert.Equal("多行 预览",separate.Preview);Assert.Equal(2,separate.UnreadCount);
    }
    [Fact] public void BadgeInsidePreviewOrNicknameIsNotReadAsUnreadCount()
    {
        var row=WeChatSessionParser.Parse("session_item_好友[8条]","好友[8条]\n正文提到[7条]\n20:45")!;
        Assert.False(row.HasUnreadMarker);Assert.Null(row.UnreadCount);
    }
    [Fact] public void UnknownIsNotZeroAndCappedCountsAreNotExact()
    {
        var row=WeChatSessionParser.Parse("session_item_好友","好友\n普通内容\n20:45")!;
        Assert.Null(row.UnreadCount);
        var capped=WeChatSessionParser.Parse("session_item_好友","好友\n[99+条] 内容\n20:45")!;
        Assert.True(capped.HasUnreadMarker);Assert.Null(capped.UnreadCount);
    }
    [Theory]
    [InlineData("unrelated","好友\n[1条] 内容\n20:45")]
    [InlineData("session_item_好友","其他人\n[1条] 内容\n20:45")]
    [InlineData("session_item_好友","好友\n[1条] 内容")]
    [InlineData("session_item_好友","好友\n[1条] 内容\n未知格式")]
    public void UnknownStructureFailsClosed(string id,string text)=>Assert.Null(WeChatSessionParser.Parse(id,text));
    [Fact] public void PreviewIsBoundedAndDoesNotIncludeTimeOrMuteLabel()
    {
        var row=Row(new string('字',200),2,true);
        Assert.True(row.Muted);Assert.Equal(120,row.Preview!.Length);Assert.DoesNotContain("20:45",row.Preview);
    }
    [Fact] public void InitialUnreadAndNewlyExposedRowsOnlyEstablishBaseline()
    {
        var tracker=new WeChatSessionChangeTracker();var row=Row();
        Assert.Empty(tracker.Observe(new[]{row},false,DateTimeOffset.Now));
        Assert.Empty(tracker.Observe(new[]{row,row with {Key="new"}},false,DateTimeOffset.Now));
        Assert.Empty(tracker.Observe(new[]{row},false,DateTimeOffset.Now));
    }
    [Fact] public void NewMessageUpdatesEmitOnceAndUseUniqueEventKeys()
    {
        var tracker=new WeChatSessionChangeTracker();var now=DateTimeOffset.Now;
        tracker.Observe(new[]{Row()},false,now);
        var first=Assert.Single(tracker.Observe(new[]{Row("新的内容",2)},false,now));
        Assert.Equal("新的内容",first.MessagePreview);Assert.Null(first.UnreadCount);
        Assert.Empty(tracker.Observe(new[]{Row("新的内容",2)},false,now));
        var next=Assert.Single(tracker.Observe(new[]{Row("新的内容",3)},false,now));
        Assert.NotEqual(first.NotificationKey,next.NotificationKey);
    }
    [Fact] public void ReadDecreasesMutedAndForegroundChangesDoNotNotify()
    {
        var tracker=new WeChatSessionChangeTracker();var now=DateTimeOffset.Now;
        tracker.Observe(new[]{Row("初始",3)},false,now);
        Assert.Empty(tracker.Observe(new[]{Row("变化",2)},false,now));
        Assert.Empty(tracker.Observe(new[]{Row("变化",3,true)},false,now));
        Assert.Empty(tracker.Observe(new[]{Row("变化",4)},false,now));
        Assert.Empty(tracker.Observe(new[]{Row("变化",5)},true,now));
        Assert.Empty(tracker.Observe(new[]{Row("变化",5)},false,now));
    }
    [Fact] public void LostAccessAndDuplicateNamesRebaselineWithoutOldUnreadNotifications()
    {
        var tracker=new WeChatSessionChangeTracker();var now=DateTimeOffset.Now;
        tracker.Observe(new[]{Row()},false,now);tracker.Reset();
        Assert.Empty(tracker.Observe(new[]{Row("变化",6)},false,now));
        Assert.Empty(tracker.Observe(new[]{Row("变化",7),Row("另一个",8)},false,now));
        Assert.Empty(tracker.Observe(new[]{Row("变化",9)},false,now));
    }
    [Fact] public void UnifiedWeChatSwitchAndCountersDoNotDisableQq()
    {
        var counter=new MessageNotificationCounter();
        var wx=new MessageNotificationSummary(MessageProvider.WeChat,DateTimeOffset.Now,"昵称",MessagePreview:"预览",NotificationKey:"wx1");
        counter.TryObserve(wx,false,true,out var visible);Assert.Equal(1,visible.NewNotificationCount);
        counter.TryObserve(wx with {NotificationKey="wx2"},false,true,out var hidden,false);
        Assert.Null(hidden.ConversationDisplayName);Assert.Null(hidden.MessagePreview);Assert.Null(hidden.NewNotificationCount);
        var qq=wx with {Provider=MessageProvider.Qq,NotificationKey="qq1"};
        counter.TryObserve(qq,false,true,out var qqVisible,false);Assert.Equal("昵称",qqVisible.ConversationDisplayName);Assert.Equal(1,qqVisible.NewNotificationCount);
        Assert.True(JsonSerializer.Deserialize<MessageNotificationPreferences>("{}")!.EnableWeChatDetailedReminders);
        Assert.False(JsonSerializer.Deserialize<MessageNotificationPreferences>(JsonSerializer.Serialize(new MessageNotificationPreferences {EnableWeChatDetailedReminders=false}))!.EnableWeChatDetailedReminders);
    }
}
