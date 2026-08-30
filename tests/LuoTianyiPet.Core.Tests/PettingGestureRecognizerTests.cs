using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class PettingGestureRecognizerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void HorizontalRoundTripsCompleteAfterMinimumDuration()
    {
        PettingGestureRecognizer recognizer = CreateRecognizer();
        recognizer.Begin(new PointerPoint(100, 100), Now);

        Assert.Equal(PettingGestureAction.None, recognizer.Move(new PointerPoint(112, 101), Now.AddMilliseconds(180)));
        Assert.Equal(PettingGestureAction.None, recognizer.Move(new PointerPoint(96, 99), Now.AddMilliseconds(360)));
        Assert.Equal(PettingGestureAction.None, recognizer.Move(new PointerPoint(110, 100), Now.AddMilliseconds(540)));
        Assert.Equal(PettingGestureAction.Completed, recognizer.Move(new PointerPoint(98, 100), Now.AddMilliseconds(620)));
        Assert.False(recognizer.IsTracking);
    }

    [Fact]
    public void MovementDoesNotCompleteBeforeMinimumDuration()
    {
        PettingGestureRecognizer recognizer = CreateRecognizer();
        recognizer.Begin(new PointerPoint(100, 100), Now);

        recognizer.Move(new PointerPoint(112, 100), Now.AddMilliseconds(100));
        recognizer.Move(new PointerPoint(96, 100), Now.AddMilliseconds(200));
        recognizer.Move(new PointerPoint(110, 100), Now.AddMilliseconds(300));

        Assert.Equal(PettingGestureAction.None, recognizer.Move(new PointerPoint(98, 100), Now.AddMilliseconds(400)));
        Assert.True(recognizer.IsTracking);
    }

    [Fact]
    public void OneWayMovementYieldsToWindowDrag()
    {
        PettingGestureRecognizer recognizer = CreateRecognizer();
        recognizer.Begin(new PointerPoint(100, 100), Now);

        Assert.Equal(
            PettingGestureAction.YieldToWindowDrag,
            recognizer.Move(new PointerPoint(132, 100), Now.AddMilliseconds(200)));
        Assert.False(recognizer.IsTracking);
    }

    [Fact]
    public void CancelPreventsLaterCompletion()
    {
        PettingGestureRecognizer recognizer = CreateRecognizer();
        recognizer.Begin(new PointerPoint(100, 100), Now);

        recognizer.Cancel();

        Assert.Equal(
            PettingGestureAction.None,
            recognizer.Move(new PointerPoint(90, 100), Now.AddSeconds(1)));
    }

    private static PettingGestureRecognizer CreateRecognizer() => new(
        TimeSpan.FromMilliseconds(600),
        40,
        2,
        30);
}
