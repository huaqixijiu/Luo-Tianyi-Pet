using LuoTianyiPet.Core;

namespace LuoTianyiPet.Platform.Windows.Tests;

public sealed class WeChatSessionSourceTests
{
    private static WeChatSessionSnapshot Snapshot(int count, bool foreground = false) =>
        new(1, foreground, true, new[] { new WeChatSessionRow("hash", "测试好友", "测试摘要", count > 0, count, false) });

    [Fact]
    public void ShortReadFailureDoesNotSwallowNextFirstUnread()
    {
        var now = DateTimeOffset.Now;
        WeChatSessionSnapshot? snapshot = Snapshot(0);
        using var source = new WindowsWeChatSessionNotificationSource(() => snapshot, () => now);
        List<MessageNotificationSummary> messages = [];
        source.NotificationReceived += (_, e) => messages.Add(e.Notification);
        source.Start(); source.Poll();
        now = now.AddMilliseconds(750); snapshot = null; source.Poll();
        now = now.AddMilliseconds(750); snapshot = Snapshot(1); source.Poll();
        var message = Assert.Single(messages);
        Assert.True(source.IsCurrent(message)); Assert.Equal("测试好友", message.ConversationDisplayName);
        snapshot = Snapshot(0); source.Poll();
        Assert.False(source.IsCurrent(message)); Assert.False(source.HasUnreadConversations);
    }

    [Fact]
    public void LongFailureRecoveryAndForegroundDoNotReplayMessages()
    {
        var now = DateTimeOffset.Now; WeChatSessionSnapshot? snapshot = Snapshot(0);
        using var source = new WindowsWeChatSessionNotificationSource(() => snapshot, () => now);
        int events = 0; source.NotificationReceived += (_, _) => events++;
        source.Start(); source.Poll(); snapshot = null; now = now.AddSeconds(4); source.Poll();
        snapshot = Snapshot(2); source.Poll(); Assert.Equal(0, events);
        snapshot = Snapshot(3, true); source.Poll();
        snapshot = Snapshot(3); source.Poll(); Assert.Equal(0, events);
        snapshot = Snapshot(4); source.Poll(); Assert.Equal(1, events);
    }

    [Fact]
    public void SlowOrStoppedReadCannotPublishLateResult()
    {
        var now = DateTimeOffset.Now; var snapshot = Snapshot(0);
        Action? duringRead = null;
        using var source = new WindowsWeChatSessionNotificationSource(() => { duringRead?.Invoke(); return snapshot; }, () => now);
        int events = 0; source.NotificationReceived += (_, _) => events++;
        source.Start(); source.Poll(); snapshot = Snapshot(1);
        duringRead = () => now = now.AddSeconds(5); source.Poll(); Assert.Equal(0, events);
        duringRead = null; source.Poll(); Assert.Equal(0, events);
        snapshot = Snapshot(2); duringRead = source.Stop; source.Poll(); Assert.Equal(0, events);
        Assert.Null(source.HasUnreadConversations);
    }
}
