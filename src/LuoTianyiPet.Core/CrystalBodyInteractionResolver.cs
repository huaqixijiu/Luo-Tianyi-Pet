namespace LuoTianyiPet.Core;

/// <summary>
/// Maps the crystal-dress artwork's user-drawn regions to animations that keep
/// the same character model on screen. It intentionally does not fall back to
/// the classic model's expression-pack reactions.
/// </summary>
public sealed class CrystalBodyInteractionResolver
{
    public const string CoverMouthAnimation = "crystal-cover-mouth";
    public const string HandHeartAnimation = "crystal-hand-heart";
    public const string TouchLegAnimation = "crystal-touch-leg";
    public const string HoldBellyAnimation = "crystal-hold-belly";
    public const string HeadPatAnimation = "crystal-headpat";
    public const string CoverEyesAnimation = "crystal-cover-eyes";
    public const string PinchCheeksAnimation = "crystal-pinch-cheeks";

    public BodyInteractionDecision Resolve(BodyRegionId region) => region switch
    {
        BodyRegionId.LeftEye or BodyRegionId.RightEye => Play(CoverEyesAnimation),
        BodyRegionId.Mouth => Play(CoverMouthAnimation),
        BodyRegionId.Face => Play(PinchCheeksAnimation),
        BodyRegionId.LeftHand or BodyRegionId.RightHand => Play(HandHeartAnimation),
        BodyRegionId.LeftFoot or BodyRegionId.RightFoot => Play(TouchLegAnimation),
        BodyRegionId.HeadAndHair => Play(HeadPatAnimation),
        BodyRegionId.OtherBody => Play(HoldBellyAnimation),
        BodyRegionId.Chest or BodyRegionId.LowerBodySensitiveArea =>
            new(BodyInteractionDecisionKind.NoAction),
        _ => throw new ArgumentOutOfRangeException(nameof(region)),
    };

    public BodyInteractionDecision ResolvePetting() => Play(HeadPatAnimation);

    private static BodyInteractionDecision Play(string animationId) =>
        new(BodyInteractionDecisionKind.PlayAnimation, animationId);
}
