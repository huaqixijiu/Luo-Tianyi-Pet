using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LuoTianyiPet.App;

internal sealed class BodyReactionMotion
{
    private readonly ScaleTransform _scale;
    private readonly TranslateTransform _translate;

    public BodyReactionMotion(ScaleTransform scale, TranslateTransform translate)
    {
        _scale = scale;
        _translate = translate;
    }

    public void PlayFor(string animationId)
    {
        if (animationId == Core.BodyInteractionResolver.HighFiveAnimation)
        {
            PlayHighFiveBounce();
        }
        else if (animationId == Core.BodyInteractionResolver.OopsAnimation)
        {
            PlayOopsShake();
        }
    }

    public void Cancel()
    {
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _translate.BeginAnimation(TranslateTransform.XProperty, null);
        _scale.ScaleX = 1;
        _scale.ScaleY = 1;
        _translate.X = 0;
    }

    private void PlayHighFiveBounce()
    {
        Cancel();
        DoubleAnimationUsingKeyFrames scale = new()
        {
            Duration = TimeSpan.FromMilliseconds(520),
            FillBehavior = FillBehavior.Stop,
        };
        scale.KeyFrames.Add(new EasingDoubleKeyFrame(0.94, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        scale.KeyFrames.Add(new EasingDoubleKeyFrame(
            1.10,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(170)),
            new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut }));
        scale.KeyFrames.Add(new EasingDoubleKeyFrame(
            0.98,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(350)),
            new CubicEase { EasingMode = EasingMode.EaseInOut }));
        scale.KeyFrames.Add(new EasingDoubleKeyFrame(
            1,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(520)),
            new CubicEase { EasingMode = EasingMode.EaseOut }));
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, scale, HandoffBehavior.SnapshotAndReplace);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, scale, HandoffBehavior.SnapshotAndReplace);
    }

    private void PlayOopsShake()
    {
        Cancel();
        DoubleAnimationUsingKeyFrames shake = new()
        {
            Duration = TimeSpan.FromMilliseconds(560),
            FillBehavior = FillBehavior.Stop,
        };
        int[] offsets = [0, -8, 8, -7, 7, -5, 5, -3, 3, 0];
        for (int index = 0; index < offsets.Length; index++)
        {
            shake.KeyFrames.Add(new LinearDoubleKeyFrame(
                offsets[index],
                KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(index * 60))));
        }

        _translate.BeginAnimation(
            TranslateTransform.XProperty,
            shake,
            HandoffBehavior.SnapshotAndReplace);
    }
}
