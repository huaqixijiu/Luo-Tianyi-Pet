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
        else if (animationId is Core.StartupTimeSceneResolver.MorningAnimation or
            Core.StartupTimeSceneResolver.AfternoonAnimation)
        {
            PlayGreetingFloat();
        }
        else if (animationId == Core.StartupTimeSceneResolver.LunchAnimation)
        {
            PlayLunchBounce();
        }
        else if (animationId == Core.StartupTimeSceneResolver.NightAnimation)
        {
            PlayNightBreathing();
        }
        else if (animationId == "resonance-awake-pop")
        {
            PlayAwakePop();
        }
        else if (animationId == Core.PetVisualState.SleepingAnimation)
        {
            PlaySleepingFloat();
        }
        else if (animationId == Core.PetVisualState.MusicPausedAnimation)
        {
            PlayPauseBreathing();
        }
        else if (animationId == "codename-curious-sway")
        {
            PlayCuriousSway();
        }
    }

    public void Cancel()
    {
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _translate.BeginAnimation(TranslateTransform.XProperty, null);
        _translate.BeginAnimation(TranslateTransform.YProperty, null);
        _scale.ScaleX = 1;
        _scale.ScaleY = 1;
        _translate.X = 0;
        _translate.Y = 0;
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

    private void PlayGreetingFloat()
    {
        Cancel();
        DoubleAnimationUsingKeyFrames motion = new()
        {
            Duration = TimeSpan.FromMilliseconds(1450),
            FillBehavior = FillBehavior.Stop,
        };
        motion.KeyFrames.Add(new EasingDoubleKeyFrame(
            3,
            KeyTime.FromTimeSpan(TimeSpan.Zero)));
        motion.KeyFrames.Add(new EasingDoubleKeyFrame(
            -7,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(650)),
            new SineEase { EasingMode = EasingMode.EaseInOut }));
        motion.KeyFrames.Add(new EasingDoubleKeyFrame(
            0,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1450)),
            new SineEase { EasingMode = EasingMode.EaseInOut }));
        _translate.BeginAnimation(
            TranslateTransform.YProperty,
            motion,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void PlayLunchBounce()
    {
        Cancel();
        DoubleAnimationUsingKeyFrames scale = new()
        {
            Duration = TimeSpan.FromMilliseconds(1050),
            FillBehavior = FillBehavior.Stop,
        };
        scale.KeyFrames.Add(new EasingDoubleKeyFrame(0.96, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        scale.KeyFrames.Add(new EasingDoubleKeyFrame(
            1.09,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(310)),
            new BackEase { Amplitude = 0.25, EasingMode = EasingMode.EaseOut }));
        scale.KeyFrames.Add(new EasingDoubleKeyFrame(
            0.99,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(650)),
            new SineEase { EasingMode = EasingMode.EaseInOut }));
        scale.KeyFrames.Add(new EasingDoubleKeyFrame(
            1,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1050)),
            new SineEase { EasingMode = EasingMode.EaseOut }));
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, scale, HandoffBehavior.SnapshotAndReplace);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, scale, HandoffBehavior.SnapshotAndReplace);
    }

    private void PlayNightBreathing()
    {
        Cancel();
        DoubleAnimationUsingKeyFrames scale = new()
        {
            Duration = TimeSpan.FromMilliseconds(1900),
            FillBehavior = FillBehavior.Stop,
        };
        scale.KeyFrames.Add(new EasingDoubleKeyFrame(0.98, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        scale.KeyFrames.Add(new EasingDoubleKeyFrame(
            1.025,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(900)),
            new SineEase { EasingMode = EasingMode.EaseInOut }));
        scale.KeyFrames.Add(new EasingDoubleKeyFrame(
            1,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1900)),
            new SineEase { EasingMode = EasingMode.EaseInOut }));
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, scale, HandoffBehavior.SnapshotAndReplace);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, scale, HandoffBehavior.SnapshotAndReplace);
    }

    private void PlayAwakePop()
    {
        Cancel();
        DoubleAnimationUsingKeyFrames scale = new()
        {
            Duration = TimeSpan.FromMilliseconds(700),
            FillBehavior = FillBehavior.Stop,
        };
        scale.KeyFrames.Add(new EasingDoubleKeyFrame(0.82, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        scale.KeyFrames.Add(new EasingDoubleKeyFrame(
            1.10,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300)),
            new BackEase { Amplitude = 0.4, EasingMode = EasingMode.EaseOut }));
        scale.KeyFrames.Add(new EasingDoubleKeyFrame(
            1,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700)),
            new SineEase { EasingMode = EasingMode.EaseOut }));
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, scale, HandoffBehavior.SnapshotAndReplace);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, scale, HandoffBehavior.SnapshotAndReplace);
    }

    private void PlaySleepingFloat()
    {
        Cancel();
        DoubleAnimation floatMotion = new(-3, 3, TimeSpan.FromMilliseconds(1800))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        _translate.BeginAnimation(
            TranslateTransform.YProperty,
            floatMotion,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void PlayPauseBreathing()
    {
        Cancel();
        DoubleAnimation scale = new(0.985, 1.018, TimeSpan.FromMilliseconds(1500))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        DoubleAnimation floatMotion = new(2, -3, TimeSpan.FromMilliseconds(1500))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, scale, HandoffBehavior.SnapshotAndReplace);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, scale, HandoffBehavior.SnapshotAndReplace);
        _translate.BeginAnimation(
            TranslateTransform.YProperty,
            floatMotion,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void PlayCuriousSway()
    {
        Cancel();
        DoubleAnimationUsingKeyFrames sway = new()
        {
            Duration = TimeSpan.FromMilliseconds(1800),
            FillBehavior = FillBehavior.Stop,
        };
        double[] offsets = [0, -5, 5, -4, 4, -2, 2, 0];
        for (int index = 0; index < offsets.Length; index++)
        {
            sway.KeyFrames.Add(new EasingDoubleKeyFrame(
                offsets[index],
                KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(index * 250)),
                new SineEase { EasingMode = EasingMode.EaseInOut }));
        }

        _translate.BeginAnimation(
            TranslateTransform.XProperty,
            sway,
            HandoffBehavior.SnapshotAndReplace);
    }
}
