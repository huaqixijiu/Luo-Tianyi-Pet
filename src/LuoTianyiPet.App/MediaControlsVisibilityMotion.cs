using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LuoTianyiPet.App;

internal sealed class MediaControlsVisibilityMotion
{
    private static readonly Duration ShowDuration = new(TimeSpan.FromMilliseconds(170));
    private static readonly Duration HideDuration = new(TimeSpan.FromMilliseconds(120));
    private readonly FrameworkElement _surface;
    private readonly TranslateTransform _translate;
    private long _animationVersion;

    public MediaControlsVisibilityMotion(
        FrameworkElement surface,
        TranslateTransform translate)
    {
        _surface = surface;
        _translate = translate;
    }

    public void Show(bool animate = true)
    {
        long version = ++_animationVersion;
        StopAnimations();
        _surface.Visibility = Visibility.Visible;
        _surface.IsHitTestVisible = true;

        if (!animate)
        {
            SetVisibleState();
            return;
        }

        if (_surface.Opacity <= 0.01)
        {
            _surface.Opacity = 0;
            _translate.Y = 8;
        }

        CubicEase ease = new() { EasingMode = EasingMode.EaseOut };
        DoubleAnimation opacity = new()
        {
            To = 1,
            Duration = ShowDuration,
            EasingFunction = ease,
        };
        DoubleAnimation movement = new()
        {
            To = 0,
            Duration = ShowDuration,
            EasingFunction = ease,
        };
        opacity.Completed += (_, _) =>
        {
            if (version == _animationVersion)
            {
                StopAnimations();
                SetVisibleState();
            }
        };
        _surface.BeginAnimation(UIElement.OpacityProperty, opacity);
        _translate.BeginAnimation(TranslateTransform.YProperty, movement);
    }

    public void Hide(bool animate = true)
    {
        long version = ++_animationVersion;
        StopAnimations();
        _surface.IsHitTestVisible = false;

        if (!animate || _surface.Visibility != Visibility.Visible)
        {
            SetHiddenState();
            return;
        }

        CubicEase ease = new() { EasingMode = EasingMode.EaseIn };
        DoubleAnimation opacity = new()
        {
            To = 0,
            Duration = HideDuration,
            EasingFunction = ease,
        };
        DoubleAnimation movement = new()
        {
            To = 7,
            Duration = HideDuration,
            EasingFunction = ease,
        };
        opacity.Completed += (_, _) =>
        {
            if (version == _animationVersion)
            {
                StopAnimations();
                SetHiddenState();
            }
        };
        _surface.BeginAnimation(UIElement.OpacityProperty, opacity);
        _translate.BeginAnimation(TranslateTransform.YProperty, movement);
    }

    public void Cancel()
    {
        ++_animationVersion;
        StopAnimations();
    }

    private void SetVisibleState()
    {
        _surface.Opacity = 1;
        _translate.Y = 0;
        _surface.Visibility = Visibility.Visible;
        _surface.IsHitTestVisible = true;
    }

    private void SetHiddenState()
    {
        _surface.Opacity = 0;
        _translate.Y = 8;
        _surface.Visibility = Visibility.Collapsed;
        _surface.IsHitTestVisible = false;
    }

    private void StopAnimations()
    {
        _surface.BeginAnimation(UIElement.OpacityProperty, null);
        _translate.BeginAnimation(TranslateTransform.YProperty, null);
    }
}
