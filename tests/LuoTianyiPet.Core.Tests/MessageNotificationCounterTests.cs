using LuoTianyiPet.Core;
namespace LuoTianyiPet.Core.Tests;
public sealed class MessageNotificationCounterTests
{
    private static MessageNotificationSummary Toast(string key) => new(MessageProvider.Qq,
        DateTimeOffset.UtcNow, "测试昵称", MessagePreview: "测试预览", NotificationKey: key);
    [Fact] public void BurstCountsDistinctToastsButNotSnapshotRepeatsOrShellFlashes()
    {
        var counter = new MessageNotificationCounter();
        Assert.True(counter.TryObserve(Toast("1"), false, true, out var first));
        Assert.Equal(1, first.NewNotificationCount);
        Assert.False(counter.TryObserve(Toast("1"), false, true, out _));
        Assert.True(counter.TryObserve(new(MessageProvider.Qq, DateTimeOffset.UtcNow), false, true, out var shell));
        Assert.Null(shell.NewNotificationCount);
        Assert.True(counter.TryObserve(Toast("2"), false, true, out var second));
        Assert.Equal(2, second.NewNotificationCount);
        Assert.Null(second.UnreadCount);
    }
    [Fact] public void ForegroundClearsCountAndDisabledDetailsAreRemovedTogether()
    {
        var counter = new MessageNotificationCounter();
        counter.TryObserve(Toast("1"), false, true, out _);
        counter.TryObserve(Toast("2"), true, true, out _);
        counter.TryObserve(Toast("3"), false, true, out var result);
        Assert.Equal(1, result.NewNotificationCount);
        counter.Clear();
        counter.TryObserve(Toast("4"), false, false, out result);
        Assert.Null(result.ConversationDisplayName);
        Assert.Null(result.MessagePreview);
        Assert.Null(result.NewNotificationCount);
        counter.TryObserve(Toast("5"), false, true, out result);
        Assert.Equal(1, result.NewNotificationCount);
    }
    [Fact] public void DifferentToastKeysInsideThreeSecondsAreBothEligible()
    {
        var coordinator = new MessageNotificationCoordinator(TimeSpan.FromSeconds(3));
        var first = Toast("1");
        Assert.Equal(MessageNotificationDecision.Show, coordinator.Observe(first, false, true));
        Assert.Equal(MessageNotificationDecision.Show, coordinator.Observe(first with { NotificationKey = "2" }, false, true));
    }
    [Fact] public void DeferredBurstKeepsLatestPreviewAndAccumulatedCount()
    {
        var counter = new MessageNotificationCounter();
        var coordinator = new MessageNotificationCoordinator(TimeSpan.FromSeconds(3));
        foreach (string key in new[]{"1","2","3"})
        {
            counter.TryObserve(Toast(key) with { MessagePreview = "预览"+key }, false, true, out var summary);
            Assert.Equal(MessageNotificationDecision.Deferred, coordinator.Observe(summary, false, false));
        }
        Assert.True(coordinator.TryTakePending(_=>false, out MessageNotificationSummary pending));
        Assert.Equal(3,pending.NewNotificationCount);
        Assert.Equal("预览3",pending.MessagePreview);
    }
}
