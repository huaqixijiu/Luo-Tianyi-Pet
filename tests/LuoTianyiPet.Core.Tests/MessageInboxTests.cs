namespace LuoTianyiPet.Core.Tests;

public sealed class MessageInboxTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Now;
    private static MessageNotificationSummary Message(string id, string name="好友", MessageProvider provider=MessageProvider.WeChat) =>
        new(provider,Now, name, MessagePreview:"消息摘要",NotificationKey:id);
    [Fact] public void MergeTotalsIgnoreAndNewMessagesRemainConsistent()
    {
        var inbox=new MessageInbox(); inbox.Add(Message("1")); inbox.Add(Message("2")); inbox.Add(Message("3","群聊"));
        Assert.Equal(3,inbox.Total(MessageProvider.WeChat)); Assert.Equal(2,inbox.Rows(MessageProvider.WeChat).Count);
        var row=inbox.Rows(MessageProvider.WeChat).Single(r=>r.Name=="好友"); inbox.Ignore(row.Key);
        Assert.False(inbox.Add(Message("2"))); Assert.Equal(1,inbox.Total(MessageProvider.WeChat));
        inbox.Add(Message("4")); Assert.Equal(2,inbox.Total(MessageProvider.WeChat));
        Assert.Equal(inbox.Rows(MessageProvider.WeChat).Sum(r=>(long)r.Count),inbox.Total(MessageProvider.WeChat));
    }
    [Fact] public void ClearOneAppDoesNotAffectOtherAppOrPermitReplay()
    {
        var inbox=new MessageInbox(); inbox.Add(Message("wx")); inbox.Add(Message("qq",provider:MessageProvider.Qq));
        inbox.Clear(MessageProvider.WeChat,Now); Assert.Equal(1,inbox.Total(MessageProvider.Qq));
        Assert.False(inbox.Add(Message("wx"))); Assert.False(inbox.Add(Message("late")));
        Assert.True(inbox.Add(Message("new") with {OccurredAt=Now.AddSeconds(1)}));
    }
    [Fact] public void ShellThenDetailsIsOneReminderAndCrossSourceWechatDoesNotDoubleCount()
    {
        var inbox=new MessageInbox(); inbox.Add(new(MessageProvider.WeChat,Now));
        inbox.Add(Message("toast")); Assert.Equal(1,inbox.Total(MessageProvider.WeChat));
        inbox.Add(Message("uia") with {WeChatSessionKey="hash"}); Assert.Equal(1,inbox.Total(MessageProvider.WeChat));
        inbox.Add(Message("uia2") with {WeChatSessionKey="hash"}); Assert.Equal(2,inbox.Total(MessageProvider.WeChat));
    }
    [Fact] public void NewestFirstAndUnifiedPrivacySwitchKeepTotals()
    {
        var inbox=new MessageInbox(); inbox.Add(Message("1","甲")); inbox.Add(Message("2","乙") with {OccurredAt=Now.AddSeconds(1)});
        Assert.Equal("乙",inbox.Rows(MessageProvider.WeChat)[0].Name);
        inbox.HideDetails(MessageProvider.WeChat); var row=Assert.Single(inbox.Rows(MessageProvider.WeChat));
        Assert.Equal(2,row.Count); Assert.Equal("有新消息",row.Preview); Assert.Null(row.Latest.ConversationDisplayName);
        Assert.Equal("99+",MessageInbox.Badge(100));
    }
    [Fact] public void HoverDelayBridgeLeaveAndPinAreDeterministic()
    {
        var state=new MessageInboxInteraction(); state.Move(MessageProvider.WeChat,false,Now);
        state.Move(MessageProvider.WeChat,false,Now.AddMilliseconds(299)); Assert.Null(state.OpenProvider);
        state.Move(MessageProvider.WeChat,false,Now.AddMilliseconds(300)); Assert.Equal(MessageProvider.WeChat,state.OpenProvider);
        state.Move(null,true,Now.AddSeconds(1)); Assert.NotNull(state.OpenProvider);
        state.Move(null,false,Now.AddSeconds(2)); state.Move(null,false,Now.AddMilliseconds(2499)); Assert.NotNull(state.OpenProvider);
        state.Move(null,false,Now.AddMilliseconds(2500)); Assert.Null(state.OpenProvider);
        state.Click(MessageProvider.Qq); state.Move(null,false,Now.AddSeconds(5)); Assert.True(state.Pinned);
        state.Click(MessageProvider.Qq); Assert.Null(state.OpenProvider);
        state.Click(MessageProvider.WeChat); state.OutsideClick(); Assert.Null(state.OpenProvider);
    }
    [Theory] [InlineData(.75)] [InlineData(1)] [InlineData(1.25)] [InlineData(1.5)] [InlineData(2)]
    public void AllLayoutsStayInsideMonitorAndReserveOnlyExistingIcons(double scale)
    {
        var work=new DesktopRectangle(-1920,0,1920,1040);
        foreach(var side in new[]{MessageRailSide.Left,MessageRailSide.Right})
        foreach(var y in new[]{-20.0,0,1000})
        foreach(int count in new[]{1,2})
        {
            var shape=new DesktopRectangle(side==MessageRailSide.Left?-400:-1700,y,160,238);
            var layout=MessageRailPlacement.Place(shape,y+50,work,side,count,9,count-1,scale);
            Assert.True(layout.Icons.Left>=work.Left && layout.Icons.Right<=work.Right);
            Assert.True(layout.Panel.Left>=work.Left && layout.Panel.Right<=work.Right);
            Assert.True(layout.Panel.Top>=work.Top && layout.Panel.Bottom<=work.Bottom);
            Assert.Equal((count*30+4)*scale,layout.Icons.Height);
        }
    }
    [Fact] public void HysteresisDragAndOpenPanelFreezeDirection()
    {
        var work=new DesktopRectangle(0,0,1000,800);
        var center=new DesktopRectangle(460,0,80,200); var right=new DesktopRectangle(800,0,100,200);
        Assert.Equal(MessageRailSide.Right,MessageRailPlacement.SideFor(center,work,MessageRailSide.Right,false));
        Assert.Equal(MessageRailSide.Right,MessageRailPlacement.SideFor(right,work,MessageRailSide.Right,true));
        Assert.Equal(MessageRailSide.Left,MessageRailPlacement.SideFor(right,work,MessageRailSide.Right,false));
    }
    [Fact] public void OutOfOrderEventsCountButDoNotReplaceLatestPreview()
    {
        var inbox=new MessageInbox();inbox.Add(Message("new") with {OccurredAt=Now.AddSeconds(1),MessagePreview="最新"});
        inbox.Add(Message("old"));var row=Assert.Single(inbox.Rows(MessageProvider.WeChat));
        Assert.Equal(2,row.Count);Assert.Equal("最新",row.Preview);
    }
    [Fact] public void HiddenMultiConversationCountsAreNotAttributedToOneNewSender()
    {
        var inbox=new MessageInbox();inbox.Add(Message("1","甲"));inbox.Add(Message("2","乙"));inbox.HideDetails(MessageProvider.WeChat);
        inbox.Add(Message("3","丙"));Assert.Equal(3,inbox.Total(MessageProvider.WeChat));
        Assert.Equal(1,inbox.Rows(MessageProvider.WeChat).Single(row=>row.Name=="丙").Count);
    }
}
