using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class MessageNotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 20, 0, 0, TimeSpan.FromHours(8));

    [Theory]
    [InlineData("Tencent.QQ_abc!App", "QQ", MessageProvider.Qq)]
    [InlineData("Tencent.WeChat_abc!App", "微信", MessageProvider.WeChat)]
    [InlineData("Tencent.Weixin_abc!App", "WeChat", MessageProvider.WeChat)]
    public void MatcherIdentifiesOnlyConfiguredApplicationMetadata(
        string appUserModelId,
        string displayName,
        MessageProvider expected)
    {
        MessageProviderMatcher matcher = new(new MessageNotificationPreferences());

        Assert.Equal(expected, matcher.Identify(appUserModelId, displayName));
    }

    [Fact]
    public void MatcherRejectsUnrelatedApplicationMetadata()
    {
        MessageProviderMatcher matcher = new(new MessageNotificationPreferences());

        Assert.Null(matcher.Identify("Microsoft.WindowsTerminal_abc!App", "终端"));
    }

    [Theory]
    [InlineData(MessageProvider.Qq, "QQ")]
    [InlineData(MessageProvider.Qq, "QQ.exe")]
    [InlineData(MessageProvider.WeChat, "WeChat")]
    [InlineData(MessageProvider.WeChat, "Weixin.exe")]
    public void MatcherRecognizesConfiguredForegroundProcesses(
        MessageProvider provider,
        string processName)
    {
        MessageProviderMatcher matcher = new(new MessageNotificationPreferences());

        Assert.True(matcher.IsForegroundProcess(provider, processName));
        Assert.False(matcher.IsForegroundProcess(provider, "notepad.exe"));
    }

    [Fact]
    public void CoordinatorShowsSafeBackgroundNotification()
    {
        MessageNotificationCoordinator coordinator = new(TimeSpan.FromSeconds(3));

        MessageNotificationDecision decision = coordinator.Observe(
            MessageProvider.Qq,
            Now,
            sourceIsForeground: false,
            canShow: true);

        Assert.Equal(MessageNotificationDecision.Show, decision);
        Assert.False(coordinator.HasPending);
    }

    [Fact]
    public void CoordinatorSuppressesSameSourceInsideDuplicateWindow()
    {
        MessageNotificationCoordinator coordinator = new(TimeSpan.FromSeconds(3));
        coordinator.Observe(MessageProvider.Qq, Now, false, true);

        MessageNotificationDecision decision = coordinator.Observe(
            MessageProvider.Qq,
            Now.AddMilliseconds(2999),
            sourceIsForeground: false,
            canShow: true);

        Assert.Equal(MessageNotificationDecision.IgnoredDuplicate, decision);
    }

    [Fact]
    public void CoordinatorAllowsDifferentSourcesInsideDuplicateWindow()
    {
        MessageNotificationCoordinator coordinator = new(TimeSpan.FromSeconds(3));
        coordinator.Observe(MessageProvider.Qq, Now, false, true);

        MessageNotificationDecision decision = coordinator.Observe(
            MessageProvider.WeChat,
            Now.AddSeconds(1),
            sourceIsForeground: false,
            canShow: true);

        Assert.Equal(MessageNotificationDecision.Show, decision);
    }

    [Fact]
    public void CoordinatorIgnoresNotificationWhenSourceIsForeground()
    {
        MessageNotificationCoordinator coordinator = new(TimeSpan.FromSeconds(3));

        MessageNotificationDecision decision = coordinator.Observe(
            MessageProvider.WeChat,
            Now,
            sourceIsForeground: true,
            canShow: true);

        Assert.Equal(MessageNotificationDecision.IgnoredSourceForeground, decision);
        Assert.False(coordinator.HasPending);
    }

    [Fact]
    public void CoordinatorDefersUnsafeNotificationAndReleasesItLater()
    {
        MessageNotificationCoordinator coordinator = new(TimeSpan.FromSeconds(3));

        Assert.Equal(
            MessageNotificationDecision.Deferred,
            coordinator.Observe(MessageProvider.Qq, Now, false, canShow: false));
        Assert.True(coordinator.HasPending);

        Assert.True(coordinator.TryTakePending(_ => false, out MessageProvider provider));
        Assert.Equal(MessageProvider.Qq, provider);
        Assert.False(coordinator.HasPending);
    }

    [Fact]
    public void PendingNotificationIsClearedWhenSourceBecomesForeground()
    {
        MessageNotificationCoordinator coordinator = new(TimeSpan.FromSeconds(3));
        coordinator.Observe(MessageProvider.WeChat, Now, false, canShow: false);

        Assert.False(coordinator.TryTakePending(
            provider => provider == MessageProvider.WeChat,
            out _));
        Assert.False(coordinator.HasPending);
    }
}
