namespace LuoTianyiPet.Core;

public enum BodyInteractionDecisionKind
{
    PlayAnimation,
    PettingGestureRequired,
    SuppressedByCooldown,
}

public sealed record BodyInteractionDecision(
    BodyInteractionDecisionKind Kind,
    string? AnimationId = null);

public sealed class BodyInteractionResolver
{
    public const string KissAnimation = "resonance-kiss";
    public const string FaceAnimation = "twelfth-anniversary-stick-together";
    public const string SoftHeartAnimation = "resonance-soft-heart";
    public const string CuteAnimation = "resonance-cute";
    public const string HighFiveAnimation = "tenth-anniversary-high-five-bounce";
    public const string GuiltyAnimation = "resonance-guilty";
    public const string DarkAnimation = "resonance-dark";
    public const string OopsAnimation = "tenth-anniversary-oops-shake";
    public const string OrdinaryBodyAnimation = "twelfth-anniversary-hug";

    private static readonly TimeSpan SensitiveRepeatWindow = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan SensitiveCooldown = TimeSpan.FromSeconds(10);
    private DateTimeOffset? _sensitiveRepeatUntil;
    private DateTimeOffset? _sensitiveCooldownUntil;

    public BodyInteractionResolver(Func<int, int>? selectIndex = null)
    {
        _ = selectIndex;
    }

    public BodyInteractionDecision Resolve(BodyRegionId region, DateTimeOffset now) => region switch
    {
        BodyRegionId.LeftEye or BodyRegionId.RightEye => Play(SoftHeartAnimation),
        BodyRegionId.Mouth => Play(KissAnimation),
        BodyRegionId.Face => Play(FaceAnimation),
        BodyRegionId.LeftHand or BodyRegionId.RightHand => Play(HighFiveAnimation),
        BodyRegionId.Chest or BodyRegionId.LowerBodySensitiveArea => ResolveSensitiveRegion(now),
        BodyRegionId.LeftFoot or BodyRegionId.RightFoot => Play(OopsAnimation),
        BodyRegionId.HeadAndHair => new(BodyInteractionDecisionKind.PettingGestureRequired),
        BodyRegionId.OtherBody => Play(OrdinaryBodyAnimation),
        _ => throw new ArgumentOutOfRangeException(nameof(region)),
    };

    public BodyInteractionDecision ResolvePetting() => Play(CuteAnimation);

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

    private static BodyInteractionDecision Play(string animationId) =>
        new(BodyInteractionDecisionKind.PlayAnimation, animationId);
}
