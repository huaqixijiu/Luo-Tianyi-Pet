namespace LuoTianyiPet.Core;

public enum BodyInteractionDecisionKind
{
    PlayAnimation,
    PettingGestureRequired,
    SuppressedByCooldown,
    NoAction,
}

public sealed record BodyInteractionDecision(
    BodyInteractionDecisionKind Kind,
    string? AnimationId = null,
    bool MirrorHorizontally = false);

public sealed class BodyInteractionResolver
{
    public const string KissAnimation = "resonance-kiss";
    public const string FaceAnimation = "twelfth-anniversary-stick-together";
    public const string LeftFaceAnimation = "twelfth-anniversary-charge";
    public const string SoftHeartAnimation = "resonance-soft-heart";
    public const string RepeatedEyeAnimation = "twelfth-anniversary-cry";
    public const string HeadPatAnimation = "guoyue-headpat";
    public const string HighFiveAnimation = "tenth-anniversary-high-five-bounce";
    public const string GuiltyAnimation = "resonance-guilty";
    public const string DarkAnimation = "resonance-dark";
    public const string OopsAnimation = "tenth-anniversary-oops-shake";
    public const string RepeatedFootAnimation = "twelfth-anniversary-stop";
    public const string HugAnimation = "twelfth-anniversary-hug";
    public static IReadOnlyList<string> OrdinaryBodyAnimations { get; } =
    [
        HugAnimation,
    ];

    /// <summary>
    /// Returns the deliberately restrained playback rate used by the classic
    /// model's expression-pack reactions. Each rate is tuned to the gesture:
    /// quick surprise actions stay brisk, while affectionate reactions get a
    /// little more time to read. Unknown animations keep their authored speed.
    /// </summary>
    public static double ResolvePlaybackRate(string animationId) => animationId switch
    {
        SoftHeartAnimation => 0.72,
        KissAnimation => 0.82,
        FaceAnimation => 0.80,
        LeftFaceAnimation => 0.80,
        HeadPatAnimation => 0.60,
        HighFiveAnimation => 0.68,
        GuiltyAnimation => 0.80,
        DarkAnimation => 0.90,
        OopsAnimation => 0.68,
        RepeatedEyeAnimation => 0.80,
        RepeatedFootAnimation => 0.80,
        HugAnimation => 0.80,
        _ => 1.0,
    };

    private static readonly TimeSpan SensitiveRepeatWindow = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan SensitiveCooldown = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PairedRegionRepeatWindow = TimeSpan.FromSeconds(4);
    private DateTimeOffset? _sensitiveRepeatUntil;
    private DateTimeOffset? _sensitiveCooldownUntil;
    private DateTimeOffset? _eyeRepeatUntil;
    private DateTimeOffset? _footRepeatUntil;
    private readonly Func<int, int> _selectIndex;
    private int? _lastOrdinaryIndex;

    public BodyInteractionResolver(Func<int, int>? selectIndex = null)
    {
        _selectIndex = selectIndex ?? Random.Shared.Next;
    }

    public BodyInteractionDecision Resolve(
        BodyRegionId region,
        DateTimeOffset now,
        double normalizedPointerX = 0.5)
    {
        if (region is not BodyRegionId.LeftEye and not BodyRegionId.RightEye)
        {
            _eyeRepeatUntil = null;
        }

        if (region is not BodyRegionId.LeftFoot and not BodyRegionId.RightFoot)
        {
            _footRepeatUntil = null;
        }

        return region switch
        {
            BodyRegionId.LeftEye => ResolveEye(now, mirrorHorizontally: false),
            BodyRegionId.RightEye => ResolveEye(now, mirrorHorizontally: true),
            BodyRegionId.Mouth => Play(KissAnimation),
            BodyRegionId.Face => normalizedPointerX < 0.5
                ? Play(LeftFaceAnimation)
                : Play(FaceAnimation),
            BodyRegionId.LeftHand => Play(HighFiveAnimation),
            BodyRegionId.RightHand => Play(HighFiveAnimation, mirrorHorizontally: true),
            BodyRegionId.Chest or BodyRegionId.LowerBodySensitiveArea => ResolveSensitiveRegion(now),
            BodyRegionId.LeftFoot => ResolveFoot(now, mirrorHorizontally: true),
            BodyRegionId.RightFoot => ResolveFoot(now, mirrorHorizontally: false),
            BodyRegionId.HeadAndHair => new(BodyInteractionDecisionKind.PettingGestureRequired),
            BodyRegionId.OtherBody => ResolveOrdinaryBody(),
            _ => throw new ArgumentOutOfRangeException(nameof(region)),
        };
    }

    public BodyInteractionDecision ResolvePetting()
    {
        ResetConsecutivePairs();
        return Play(HeadPatAnimation);
    }

    public void ResetConsecutivePairs()
    {
        _eyeRepeatUntil = null;
        _footRepeatUntil = null;
    }

    private BodyInteractionDecision ResolveEye(DateTimeOffset now, bool mirrorHorizontally)
    {
        if (_eyeRepeatUntil is DateTimeOffset repeatUntil && now <= repeatUntil)
        {
            _eyeRepeatUntil = null;
            return Play(RepeatedEyeAnimation);
        }

        _eyeRepeatUntil = now + PairedRegionRepeatWindow;
        return Play(SoftHeartAnimation, mirrorHorizontally);
    }

    private BodyInteractionDecision ResolveFoot(DateTimeOffset now, bool mirrorHorizontally)
    {
        if (_footRepeatUntil is DateTimeOffset repeatUntil && now <= repeatUntil)
        {
            _footRepeatUntil = null;
            return Play(RepeatedFootAnimation);
        }

        _footRepeatUntil = now + PairedRegionRepeatWindow;
        return Play(OopsAnimation, mirrorHorizontally);
    }

    private BodyInteractionDecision ResolveOrdinaryBody()
    {
        if (OrdinaryBodyAnimations.Count == 1)
        {
            _lastOrdinaryIndex = 0;
            return Play(OrdinaryBodyAnimations[0]);
        }

        int selectableCount = OrdinaryBodyAnimations.Count - (_lastOrdinaryIndex.HasValue ? 1 : 0);
        int selected = _selectIndex(selectableCount);
        if (selected < 0 || selected >= selectableCount)
        {
            throw new InvalidOperationException("The ordinary body animation selector returned an invalid index.");
        }

        if (_lastOrdinaryIndex is int previous && selected >= previous)
        {
            selected++;
        }

        _lastOrdinaryIndex = selected;
        return Play(OrdinaryBodyAnimations[selected]);
    }

    private BodyInteractionDecision ResolveSensitiveRegion(DateTimeOffset now)
    {
        if (_sensitiveCooldownUntil is DateTimeOffset cooldownUntil && now < cooldownUntil)
        {
            return new(BodyInteractionDecisionKind.SuppressedByCooldown);
        }

        _sensitiveCooldownUntil = null;
        if (_sensitiveRepeatUntil is DateTimeOffset repeatUntil && now <= repeatUntil)
        {
            _sensitiveRepeatUntil = null;
            _sensitiveCooldownUntil = now + SensitiveCooldown;
            return Play(DarkAnimation);
        }

        _sensitiveRepeatUntil = now + SensitiveRepeatWindow;
        return Play(GuiltyAnimation);
    }

    private static BodyInteractionDecision Play(
        string animationId,
        bool mirrorHorizontally = false) =>
        new(
            BodyInteractionDecisionKind.PlayAnimation,
            animationId,
            mirrorHorizontally);
}
