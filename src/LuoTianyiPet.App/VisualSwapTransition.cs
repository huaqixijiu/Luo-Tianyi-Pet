using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LuoTianyiPet.App;

internal sealed class VisualSwapTransition
{
    private readonly UIElement _visual;
    private readonly ScaleTransform _visualScale;
    private readonly UIElement _flash;
    private readonly ScaleTransform _flashScale;
    private int _version;

    public VisualSwapTransition(
        UIElement visual,
        ScaleTransform visualScale,
        UIElement flash,
        ScaleTransform flashScale)
    {
        _visual = visual;
        _visualScale = visualScale;
        _flash = flash;
        _flashScale = flashScale;
    }

    public async Task<bool> PlayAsync(Action swapAtCoveredMidpoint)
    {
        ArgumentNullException.ThrowIfNull(swapAtCoveredMidpoint);
        _visual.Dispatcher.VerifyAccess();
        int version = ++_version;
        ResetVisuals();

        Animate(_visual, UIElement.OpacityProperty, 1, 0.06, 145, EasingMode.EaseIn);
        Animate(_visualScale, ScaleTransform.ScaleXProperty, 1, 0.84, 145, EasingMode.EaseIn);
        Animate(_visualScale, ScaleTransform.ScaleYProperty, 1, 0.84, 145, EasingMode.EaseIn);
        Animate(_flash, UIElement.OpacityProperty, 0, 0.96, 145, EasingMode.EaseOut);
        Animate(_flashScale, ScaleTransform.ScaleXProperty, 0.35, 0.78, 145, EasingMode.EaseOut);
        Animate(_flashScale, ScaleTransform.ScaleYProperty, 0.35, 0.78, 145, EasingMode.EaseOut);

        await Task.Delay(145);
        if (version != _version)
        {
            return false;
        }

        ClearAnimationsAndSet(_visual, UIElement.OpacityProperty, 0.06);
        ClearAnimationsAndSet(_visualScale, ScaleTransform.ScaleXProperty, 0.84);
        ClearAnimationsAndSet(_visualScale, ScaleTransform.ScaleYProperty, 0.84);
        ClearAnimationsAndSet(_flash, UIElement.OpacityProperty, 0.96);
        ClearAnimationsAndSet(_flashScale, ScaleTransform.ScaleXProperty, 0.78);
        ClearAnimationsAndSet(_flashScale, ScaleTransform.ScaleYProperty, 0.78);

        swapAtCoveredMidpoint();

        Animate(_visual, UIElement.OpacityProperty, 0.06, 1, 235, EasingMode.EaseOut);
        Animate(_visualScale, ScaleTransform.ScaleXProperty, 0.84, 1.04, 190, EasingMode.EaseOut);
        Animate(_visualScale, ScaleTransform.ScaleYProperty, 0.84, 1.04, 190, EasingMode.EaseOut);
        Animate(_flash, UIElement.OpacityProperty, 0.96, 0, 260, EasingMode.EaseIn);
        Animate(_flashScale, ScaleTransform.ScaleXProperty, 0.78, 1.22, 260, EasingMode.EaseOut);
        Animate(_flashScale, ScaleTransform.ScaleYProperty, 0.78, 1.22, 260, EasingMode.EaseOut);

        await Task.Delay(190);
        if (version != _version)
        {
            return false;
        }

        ClearAnimationsAndSet(_visualScale, ScaleTransform.ScaleXProperty, 1.04);
        ClearAnimationsAndSet(_visualScale, ScaleTransform.ScaleYProperty, 1.04);
        Animate(_visualScale, ScaleTransform.ScaleXProperty, 1.04, 1, 90, EasingMode.EaseOut);
        Animate(_visualScale, ScaleTransform.ScaleYProperty, 1.04, 1, 90, EasingMode.EaseOut);

        await Task.Delay(90);
        if (version != _version)
        {
            return false;
        }

        ResetVisuals();
        return true;
    }

    public void Cancel()
    {
        _visual.Dispatcher.VerifyAccess();
        _version++;
        ResetVisuals();
    }

    private void ResetVisuals()
    {
        ClearAnimationsAndSet(_visual, UIElement.OpacityProperty, 1);
        ClearAnimationsAndSet(_visualScale, ScaleTransform.ScaleXProperty, 1);
        ClearAnimationsAndSet(_visualScale, ScaleTransform.ScaleYProperty, 1);
        ClearAnimationsAndSet(_flash, UIElement.OpacityProperty, 0);
        ClearAnimationsAndSet(_flashScale, ScaleTransform.ScaleXProperty, 0.35);
        ClearAnimationsAndSet(_flashScale, ScaleTransform.ScaleYProperty, 0.35);
    }

    private static void Animate(
        DependencyObject target,
        DependencyProperty property,
        double from,
        double to,
        int milliseconds,
        EasingMode easingMode)
    {
        CubicEase easing = new() { EasingMode = easingMode };
        DoubleAnimation animation = new(from, to, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd,
        };
        BeginAnimation(target, property, animation);
    }

    private static void ClearAnimationsAndSet(
        DependencyObject target,
        DependencyProperty property,
        double value)
    {
        BeginAnimation(target, property, null);
        target.SetValue(property, value);
    }

    private static void BeginAnimation(
        DependencyObject target,
        DependencyProperty property,
        AnimationTimeline? animation)
    {
        switch (target)
        {
            case UIElement element:
                element.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
                break;
            case Animatable animatable:
                animatable.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
                break;
            default:
                throw new ArgumentException("Target does not support WPF property animation.", nameof(target));
        }
    }
}
