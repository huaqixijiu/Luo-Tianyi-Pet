using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class OperationFeedbackTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FastLaunchHasNoTextAndSlowLaunchUpdatesOneProgressMessage()
    {
        Assert.Null(OperationFeedback.LaunchProgress(TimeSpan.FromMilliseconds(999), 1, null));
        Assert.Equal(OperationFeedback.LaunchStarting,
            OperationFeedback.LaunchProgress(TimeSpan.FromSeconds(1), 1, null));
        Assert.Equal(OperationFeedback.LaunchRetrying,
            OperationFeedback.LaunchProgress(TimeSpan.FromSeconds(3), 2, null));
        Assert.Equal(OperationFeedback.LaunchRetrying,
            OperationFeedback.LaunchProgress(TimeSpan.FromSeconds(4), 2, TimeSpan.FromMilliseconds(750)));
        Assert.Equal(OperationFeedback.PlaybackWaiting,
            OperationFeedback.LaunchProgress(TimeSpan.FromSeconds(5), 2, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void RepeatedInputsDoNotExtendCooldownAndDifferentFailuresStillAppear()
    {
        FeedbackRepeatGate gate = new();
        Assert.True(gate.TryShow(OperationFeedback.VolumeFailed, Now));
        Assert.False(gate.TryShow(OperationFeedback.VolumeFailed, Now.AddSeconds(3)));
        Assert.True(gate.TryShow(OperationFeedback.ShortcutUnavailable, Now.AddSeconds(3)));
        Assert.False(gate.TryShow(OperationFeedback.VolumeFailed, Now.AddSeconds(7)));
        Assert.True(gate.TryShow(OperationFeedback.VolumeFailed, Now.AddSeconds(8)));
    }

    [Fact]
    public void ClockCorrectionDoesNotSuppressMessagesIndefinitely()
    {
        FeedbackRepeatGate gate = new();
        Assert.True(gate.TryShow(OperationFeedback.VolumeFailed, Now));
        Assert.True(gate.TryShow(OperationFeedback.VolumeFailed, Now.AddMinutes(-1)));
    }

    [Theory]
    [InlineData(MediaCommandSendStatus.Sent)]
    [InlineData(MediaCommandSendStatus.RateLimited)]
    public void SuccessfulOrThrottledCommandsAreSilent(MediaCommandSendStatus status) =>
        Assert.Null(OperationFeedback.ForMediaCommand(status));

    [Theory]
    [InlineData(MediaCommandSendStatus.ProtectedApplicationForeground)]
    [InlineData(MediaCommandSendStatus.ForegroundCheckUnavailable)]
    public void SafetyFailuresHaveAnActionableMessage(MediaCommandSendStatus status) =>
        Assert.Equal(OperationFeedback.MediaEnvironmentUnavailable, OperationFeedback.ForMediaCommand(status));

    [Theory]
    [InlineData(MediaApplicationLaunchStatus.ProtectedApplicationForeground)]
    [InlineData(MediaApplicationLaunchStatus.ForegroundCheckUnavailable)]
    public void UnsafeLaunchHasSameExplanation(MediaApplicationLaunchStatus status) =>
        Assert.Equal(OperationFeedback.MediaEnvironmentUnavailable, OperationFeedback.ForLaunch(status));

    [Theory]
    [InlineData(ApplicationVolumeAdjustmentStatus.ProtectedApplicationForeground)]
    [InlineData(ApplicationVolumeAdjustmentStatus.ForegroundCheckUnavailable)]
    [InlineData(ApplicationVolumeAdjustmentStatus.SessionUnavailable)]
    public void VolumeFailureDoesNotSuggestKeyboardOrGlobalVolumeFallback(ApplicationVolumeAdjustmentStatus status) =>
        Assert.Equal(OperationFeedback.VolumeFailed, OperationFeedback.ForVolume(status));

    [Theory]
    [InlineData(RecycleBinOperationStatus.Failed)]
    [InlineData(RecycleBinOperationStatus.Cancelled)]
    public void InterruptedRecycleWithMovedItemsNeverClaimsAllFilesRemain(RecycleBinOperationStatus status) =>
        Assert.Equal(OperationFeedback.RecycleUnconfirmed,
            OperationFeedback.ForRecycle(new(status, 3, 1, "COMException")));

    [Fact]
    public void RecycleResultsKeepCountsAndRejectionReasons()
    {
        Assert.Equal("仅 2 个项目进入回收站，请检查其余文件",
            OperationFeedback.ForRecycle(new(RecycleBinOperationStatus.PartialFailure, 3, 2, "")));
        Assert.Equal(OperationFeedback.RecycleUnconfirmed,
            OperationFeedback.ForRecycle(new(RecycleBinOperationStatus.PartialFailure, 3, 3, "")));
        Assert.Equal("一次最多接收 100 个项目。",
            OperationFeedback.ForRecycle(new(RecycleBinOperationStatus.Rejected, 101, 0, "一次最多接收 100 个项目。")));
        Assert.Equal(OperationFeedback.RecycleCancelled,
            OperationFeedback.ForRecycle(new(RecycleBinOperationStatus.Cancelled, 3, 0, "")));
        Assert.Null(OperationFeedback.ForRecycle(new(RecycleBinOperationStatus.Success, 3, 3, "")));
    }
}
