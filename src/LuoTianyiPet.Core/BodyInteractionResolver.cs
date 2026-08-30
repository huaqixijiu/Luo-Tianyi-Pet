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
    public const string SoftHeartAnimation = "resonance-soft-heart";
    public const string CuteAnimation = "resonance-cute";
    public const string HighFiveAnimation = "tenth-anniversary-high-five-bounce";
    public const string GuiltyAnimation = "resonance-guilty";
    public const string DarkAnimation = "resonance-dark";
    public const string OopsAnimation = "tenth-anniversary-oops-shake";

    public static IReadOnlyList<string> OrdinaryAnimationPool { get; } =
    [
        "tenth-anniversary-spin-dance",
        "ninth-anniversary-thumbup",
        "eighth-anniversary-thumbup",
        "resonance-my-pick",
        "resonance-so-good",
    ];

    private static readonly TimeSpan SensitiveRepeatWindow = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan SensitiveCooldown = TimeSpan.FromSeconds(10);
    private readonly Func<int, int> _selectIndex;
    private DateTimeOffset? _sensitiveRepeatUntil;
    private DateTimeOffset? _sensitiveCooldownUntil;
    private string? _lastOrdinaryAnimation;

    public BodyInteractionResolver(Func<int, int>? selectIndex = null)
    {
        _selectIndex = selectIndex ?? Random.Shared.Next;
    }

    public BodyInteractionDecision Resolve(BodyRegionId region, DateTimeOffset now) => region switch
    {
        BodyRegionId.LeftEye or BodyRegionId.RightEye => Play(SoftHeartAnimation),
        BodyRegionId.FaceAndMouth => Play(KissAnimation),
        BodyRegionId.LeftHand or BodyRegionId.RightHand => Play(HighFiveAnimation),
        BodyRegionId.Chest or BodyRegionId.LowerBodySensitiveArea => ResolveSensitiveRegion(now),
        BodyRegionId.LeftFoot or BodyRegionId.RightFoot => Play(OopsAnimation),
        BodyRegionId.HeadAndHair => new(BodyInteractionDecisionKind.PettingGestureRequired),
        BodyRegionId.OtherBody => Play(SelectOrdinaryAnimation()),
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

    private string SelectOrdinaryAnimation()
    {
        string[] candidates = OrdinaryAnimationPool
            .Where(animation => !string.Equals(animation, _lastOrdinaryAnimation, StringComparison.Ordinal))
            .ToArray();
        int index = _selectIndex(candidates.Length);
        if (index < 0 || index >= candidates.Length)
        {
            throw new InvalidOperationException("The ordinary animation selector returned an invalid index.");
        }

        _lastOrdinaryAnimation = candidates[index];
        return _lastOrdinaryAnimation;
    }

    private static BodyInteractionDecision Play(string animationId) =>
        new(BodyInteractionDecisionKind.PlayAnimation, animationId);
}
