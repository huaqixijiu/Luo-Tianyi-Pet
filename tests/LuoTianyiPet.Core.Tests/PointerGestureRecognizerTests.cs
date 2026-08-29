using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class PointerGestureRecognizerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void SingleClickIsDelayedUntilDoubleClickWindowExpires()
    {
        PointerGestureRecognizer recognizer = CreateRecognizer();
        recognizer.Press(new PointerPoint(10, 20), 1, Now);
        recognizer.Release(new PointerPoint(10, 20), Now.AddMilliseconds(40));

        Assert.True(recognizer.HasPendingSingleClick);
        Assert.Equal(
            TimeSpan.FromMilliseconds(1),
            recognizer.TimeUntilPendingSingleClick(Now.AddMilliseconds(339)));
        Assert.Equal(
            PointerGestureActionType.None,
            recognizer.FlushPendingSingleClick(Now.AddMilliseconds(339)).Type);
        PointerGestureAction action = recognizer.FlushPendingSingleClick(Now.AddMilliseconds(340));
        Assert.Equal(PointerGestureActionType.DispatchSingleClick, action.Type);
        Assert.Equal(new PointerPoint(10, 20), action.Position);
    }

    [Fact]
    public void DoubleClickSuppressesPendingAndSecondRelease()
    {
        PointerGestureRecognizer recognizer = CreateRecognizer();
        recognizer.Press(new PointerPoint(10, 20), 1, Now);
        recognizer.Release(new PointerPoint(10, 20), Now.AddMilliseconds(30));

        PointerGestureAction secondPress = recognizer.Press(
            new PointerPoint(11, 20),
            2,
            Now.AddMilliseconds(120));
        PointerGestureAction secondRelease = recognizer.Release(
            new PointerPoint(11, 20),
            Now.AddMilliseconds(150));

        Assert.Equal(PointerGestureActionType.ToggleDisplayMode, secondPress.Type);
        Assert.Equal(PointerGestureActionType.None, secondRelease.Type);
        Assert.False(recognizer.HasPendingSingleClick);
    }

    [Fact]
    public void DragThresholdCancelsClickAndProducesOneBeginAndEnd()
    {
        PointerGestureRecognizer recognizer = CreateRecognizer();
        recognizer.Press(new PointerPoint(10, 20), 1, Now);

        Assert.Equal(
            PointerGestureActionType.None,
            recognizer.Move(new PointerPoint(15.9, 20)).Type);
        Assert.Equal(
            PointerGestureActionType.BeginDrag,
            recognizer.Move(new PointerPoint(16, 20)).Type);
        Assert.Equal(
            PointerGestureActionType.None,
            recognizer.Move(new PointerPoint(30, 20)).Type);
        Assert.Equal(
            PointerGestureActionType.EndDrag,
            recognizer.Release(new PointerPoint(30, 20), Now.AddMilliseconds(200)).Type);
        Assert.False(recognizer.HasPendingSingleClick);
    }

    [Fact]
    public void UnrelatedSecondPressDispatchesEarlierPendingSingle()
    {
        PointerGestureRecognizer recognizer = CreateRecognizer();
        recognizer.Press(new PointerPoint(10, 20), 1, Now);
        recognizer.Release(new PointerPoint(10, 20), Now.AddMilliseconds(30));

        PointerGestureAction action = recognizer.Press(
            new PointerPoint(50, 70),
            1,
            Now.AddMilliseconds(100));

        Assert.Equal(PointerGestureActionType.DispatchSingleClick, action.Type);
        Assert.Equal(new PointerPoint(10, 20), action.Position);
    }

    [Fact]
    public void CancelClearsPressedDragAndPendingState()
    {
        PointerGestureRecognizer recognizer = CreateRecognizer();
        recognizer.Press(new PointerPoint(10, 20), 1, Now);
        recognizer.Release(new PointerPoint(10, 20), Now.AddMilliseconds(30));

        recognizer.Cancel();

        Assert.False(recognizer.HasPendingSingleClick);
        Assert.False(recognizer.IsDragging);
        Assert.Equal(
            PointerGestureActionType.None,
            recognizer.FlushPendingSingleClick(Now.AddSeconds(1)).Type);
    }

    private static PointerGestureRecognizer CreateRecognizer() =>
        new(6, TimeSpan.FromMilliseconds(300));
}
