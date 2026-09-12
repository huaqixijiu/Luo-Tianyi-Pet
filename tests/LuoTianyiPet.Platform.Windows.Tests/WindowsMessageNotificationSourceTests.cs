using LuoTianyiPet.Core;

namespace LuoTianyiPet.Platform.Windows.Tests;

public sealed class WindowsMessageNotificationSourceTests
{
    [Theory]
    [InlineData(unchecked((int)0x800706BA))]
    [InlineData(unchecked((int)0x800706BE))]
    [InlineData(unchecked((int)0x80010108))]
    [InlineData(unchecked((int)0x8001010E))]
    [InlineData(unchecked((int)0x803E0105))]
    public void RecoverableRpcFailuresAreRecognizedEvenWhenProjectionUsesBaseException(int hresult)
    {
        Assert.True(WindowsMessageNotificationSource.IsRecoverablePlatformException(
            new HResultException(hresult)));
    }

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
    public void NotificationSnapshotUsesFirstObservationOnlyAsBaseline()
    {
        NotificationIdSnapshotTracker tracker = new();

        Assert.Empty(tracker.Observe(new uint[] { 10, 11 }));
        Assert.Equal(new uint[] { 12 }, tracker.Observe(new uint[] { 10, 11, 12 }));
    }

    [Fact]
    public void NotificationSnapshotTreatsReintroducedIdAsNewNotification()
    {
        NotificationIdSnapshotTracker tracker = new();
        tracker.Observe(new uint[] { 10, 11 });
        tracker.Observe(new uint[] { 10 });

        Assert.Equal(new uint[] { 11 }, tracker.Observe(new uint[] { 10, 11 }));
    }

    [Fact]
    public void NotificationSnapshotResetRequiresANewBaseline()
    {
        NotificationIdSnapshotTracker tracker = new();
        tracker.Observe(new uint[] { 10 });
        tracker.Reset();

        Assert.Empty(tracker.Observe(new uint[] { 20 }));
        Assert.Equal(new uint[] { 21 }, tracker.Observe(new uint[] { 20, 21 }));
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

    [Fact]
    public void ShellFlashMessageReturnsItsTargetWindow()
    {
        IntPtr expectedWindow = new IntPtr(4567);

        Assert.True(ShellAttentionMessageClassifier.TryGetFlashingWindow(
            new IntPtr(ShellAttentionMessageClassifier.FlashCode),
            expectedWindow,
            out IntPtr actualWindow));
        Assert.Equal(expectedWindow, actualWindow);
    }

    [Theory]
    [InlineData(6, 4567)]
    [InlineData(ShellAttentionMessageClassifier.FlashCode, 0)]
    public void NonFlashShellMessagesAreIgnored(int code, int window)
    {
        Assert.False(ShellAttentionMessageClassifier.TryGetFlashingWindow(
            new IntPtr(code),
            new IntPtr(window),
            out IntPtr actualWindow));
        Assert.Equal(IntPtr.Zero, actualWindow);
    }

    [Fact]
    public void DisabledOrAmbiguousDetailsNeverReadTextValues()
    {
        string? FailRead(int _) => throw new InvalidOperationException("must not read");
        Assert.Equal((null, null), NotificationTextSelector.Read(2, FailRead, false, true));
        Assert.Equal((null, null), NotificationTextSelector.Read(1, FailRead, true, true));
    }

    [Fact]
    public void PreviewUsesBodySlotsAndNormalizesMultilineText()
    {
        var fields = new[]{"昵称", "第一行\r\n第二行", "补充"};
        var result = NotificationTextSelector.Read(3, i => fields[i], true, true);
        Assert.Equal("昵称",result.Title);
        Assert.Equal("第一行 第二行 补充",result.Preview);
        Assert.Null(NotificationTextSelector.Read(3, i=>fields[i],true,false).Preview);
        Assert.Null(NotificationTextSelector.Read(2, i=>i==0?" ":"正文",true,true).Preview);
    }

    [Fact]
    public void PreviewIsBoundedAndDoesNotSplitEmojiSurrogatePair()
    {
        string body = new string('字',118)+"😀"+new string('字',100);
        var result = NotificationTextSelector.Read(2,i=>i==0?"昵称":body,true,true);
        Assert.True(result.Preview!.Length<=120);
        Assert.EndsWith("…",result.Preview);
        Assert.False(char.IsHighSurrogate(result.Preview[result.Preview.Length-2]));
    }

    [Fact]
    public void ReplacedToastWithSameIdAndNewCreationTimeIsANewEvent()
    {
        var tracker=new NotificationIdSnapshotTracker();
        Assert.Empty(tracker.Observe(new[]{new NotificationIdentity(1,100)}));
        Assert.Empty(tracker.Observe(new[]{new NotificationIdentity(1,100)}));
        Assert.Single(tracker.Observe(new[]{new NotificationIdentity(1,200)}));
    }

    private sealed class HResultException : Exception
    {
        public HResultException(int hresult)
        {
            HResult = hresult;
        }
    }
}
