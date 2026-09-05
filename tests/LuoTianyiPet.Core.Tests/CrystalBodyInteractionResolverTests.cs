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
    [InlineData(BodyRegionId.OtherBody, CrystalBodyInteractionResolver.HoldBellyAnimation)]
    public void ResolveMapsRegionToSameModelAnimation(BodyRegionId region, string animationId)
    {
        BodyInteractionDecision decision = new CrystalBodyInteractionResolver().Resolve(region);

        Assert.Equal(BodyInteractionDecisionKind.PlayAnimation, decision.Kind);
        Assert.Equal(animationId, decision.AnimationId);
    }

    [Theory]
    [InlineData(BodyRegionId.Chest)]
    [InlineData(BodyRegionId.LowerBodySensitiveArea)]
    public void ResolveLeavesRegionsWithoutProvidedAnimationInactive(BodyRegionId region)
    {
        BodyInteractionDecision decision = new CrystalBodyInteractionResolver().Resolve(region);

        Assert.Equal(BodyInteractionDecisionKind.NoAction, decision.Kind);
        Assert.Null(decision.AnimationId);
    }

    [Fact]
    public void ResolvePettingUsesCrystalHeadPatAnimation()
    {
        BodyInteractionDecision decision = new CrystalBodyInteractionResolver().ResolvePetting();

        Assert.Equal(BodyInteractionDecisionKind.PlayAnimation, decision.Kind);
        Assert.Equal(CrystalBodyInteractionResolver.HeadPatAnimation, decision.AnimationId);
    }
}
