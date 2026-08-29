using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LuoTianyiPet.App;

internal sealed class LandingBounceMotion
{
    private readonly TranslateTransform _transform;

    public LandingBounceMotion(TranslateTransform transform)
    {
        _transform = transform;
    }

    public void Play()
    {
        Cancel();
        DoubleAnimationUsingKeyFrames animation = new()
        {
            Duration = TimeSpan.FromMilliseconds(420),
            FillBehavior = FillBehavior.Stop,
        };
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(-11, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(
            5,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(105)),
            new CubicEase { EasingMode = EasingMode.EaseOut }));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(
            -3,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220)),
            new CubicEase { EasingMode = EasingMode.EaseInOut }));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(
            0,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(360)),
            new CubicEase { EasingMode = EasingMode.EaseOut }));
        _transform.BeginAnimation(
            TranslateTransform.YProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace);
    }

    public void Cancel()
    {
        _transform.BeginAnimation(TranslateTransform.YProperty, null);
        _transform.Y = 0;
    }
}
