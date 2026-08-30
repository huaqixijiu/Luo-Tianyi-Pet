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
    public const string HeadPatAnimation = "guoyue-headpat";
    public const string HighFiveAnimation = "tenth-anniversary-high-five-bounce";
    public const string GuiltyAnimation = "resonance-guilty";
    public const string DarkAnimation = "resonance-dark";
    public const string OopsAnimation = "tenth-anniversary-oops-shake";
    public static IReadOnlyList<string> OrdinaryBodyAnimations { get; } =
    [
        "twelfth-anniversary-hug",
        "tenth-anniversary-spin-dance",
        "ninth-anniversary-thumbup",
        "eighth-anniversary-thumbup",
        "resonance-my-pick",
        "resonance-so-good",
    ];

    private static readonly TimeSpan SensitiveRepeatWindow = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan SensitiveCooldown = TimeSpan.FromSeconds(10);
    private DateTimeOffset? _sensitiveRepeatUntil;
    private DateTimeOffset? _sensitiveCooldownUntil;
    private readonly Func<int, int> _selectIndex;
    private int? _lastOrdinaryIndex;

    public BodyInteractionResolver(Func<int, int>? selectIndex = null)
    {
        _selectIndex = selectIndex ?? Random.Shared.Next;
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
        BodyRegionId.OtherBody => ResolveOrdinaryBody(),
        _ => throw new ArgumentOutOfRangeException(nameof(region)),
    };

    public BodyInteractionDecision ResolvePetting() => Play(HeadPatAnimation);

    private BodyInteractionDecision ResolveOrdinaryBody()
    {
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

    private static BodyInteractionDecision Play(string animationId) =>
        new(BodyInteractionDecisionKind.PlayAnimation, animationId);
}
