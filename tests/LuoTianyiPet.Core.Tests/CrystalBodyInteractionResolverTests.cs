using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class CrystalBodyInteractionResolverTests
{
    [Theory]
    [InlineData(BodyRegionId.LeftEye, CrystalBodyInteractionResolver.CoverEyesAnimation)]
    [InlineData(BodyRegionId.RightEye, CrystalBodyInteractionResolver.CoverEyesAnimation)]
    [InlineData(BodyRegionId.Mouth, CrystalBodyInteractionResolver.CoverMouthAnimation)]
    [InlineData(BodyRegionId.Face, CrystalBodyInteractionResolver.PinchCheeksAnimation)]
    [InlineData(BodyRegionId.LeftHand, CrystalBodyInteractionResolver.HandHeartAnimation)]
    [InlineData(BodyRegionId.RightHand, CrystalBodyInteractionResolver.HandHeartAnimation)]
    [InlineData(BodyRegionId.LeftFoot, CrystalBodyInteractionResolver.TouchLegAnimation)]
    [InlineData(BodyRegionId.RightFoot, CrystalBodyInteractionResolver.TouchLegAnimation)]
    [InlineData(BodyRegionId.HeadAndHair, CrystalBodyInteractionResolver.HeadPatAnimation)]
    [InlineData(BodyRegionId.Chest, CrystalBodyInteractionResolver.TouchChestAnimation)]
    [InlineData(BodyRegionId.LowerBodySensitiveArea, CrystalBodyInteractionResolver.TouchSkirtAnimation)]
    [InlineData(BodyRegionId.OtherBody, CrystalBodyInteractionResolver.HoldBellyAnimation)]
    public void ResolveMapsRegionToSameModelAnimation(BodyRegionId region, string animationId)
    {
        BodyInteractionDecision decision = new CrystalBodyInteractionResolver().Resolve(region);

        Assert.Equal(BodyInteractionDecisionKind.PlayAnimation, decision.Kind);
        Assert.Equal(animationId, decision.AnimationId);
    }

    [Fact]
    public void ResolvePettingUsesCrystalHeadPatAnimation()
    {
        BodyInteractionDecision decision = new CrystalBodyInteractionResolver().ResolvePetting();

        Assert.Equal(BodyInteractionDecisionKind.PlayAnimation, decision.Kind);
        Assert.Equal(CrystalBodyInteractionResolver.HeadPatAnimation, decision.AnimationId);
    }

    [Theory]
    [InlineData(CrystalBodyInteractionResolver.CoverMouthAnimation)]
    [InlineData(CrystalBodyInteractionResolver.HandHeartAnimation)]
    [InlineData(CrystalBodyInteractionResolver.TouchLegAnimation)]
    [InlineData(CrystalBodyInteractionResolver.HoldBellyAnimation)]
    [InlineData(CrystalBodyInteractionResolver.HeadPatAnimation)]
    [InlineData(CrystalBodyInteractionResolver.CoverEyesAnimation)]
    [InlineData(CrystalBodyInteractionResolver.PinchCheeksAnimation)]
    [InlineData(CrystalBodyInteractionResolver.TouchChestAnimation)]
    [InlineData(CrystalBodyInteractionResolver.TouchSkirtAnimation)]
    [InlineData(CrystalBodyInteractionResolver.YawnAnimation)]
    public void RecognizesEveryCrystalAnimationAsInPlace(string animationId)
    {
        Assert.True(CrystalBodyInteractionResolver.IsInPlaceAnimation(animationId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("resonance-hey-hey")]
    public void DoesNotTreatOtherAnimationsAsInPlace(string? animationId)
    {
        Assert.False(CrystalBodyInteractionResolver.IsInPlaceAnimation(animationId));
    }
}
