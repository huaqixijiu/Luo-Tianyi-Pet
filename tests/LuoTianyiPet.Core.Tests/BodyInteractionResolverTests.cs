using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class BodyInteractionResolverTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.FromHours(8));

    [Theory]
    [InlineData(BodyRegionId.RightEye, BodyInteractionResolver.SoftHeartAnimation)]
    [InlineData(BodyRegionId.Mouth, BodyInteractionResolver.KissAnimation)]
    [InlineData(BodyRegionId.LeftHand, BodyInteractionResolver.HighFiveAnimation)]
    [InlineData(BodyRegionId.RightFoot, BodyInteractionResolver.OopsAnimation)]
    public void FixedRegionsResolveToConfirmedAnimations(BodyRegionId region, string expected)
    {
        BodyInteractionDecision result = new BodyInteractionResolver().Resolve(region, Now);

        Assert.Equal(BodyInteractionDecisionKind.PlayAnimation, result.Kind);
        Assert.Equal(expected, result.AnimationId);
    }

    [Fact]
    public void HeadClickWaitsForPettingGesture()
    {
        BodyInteractionResolver resolver = new();

        Assert.Equal(
            BodyInteractionDecisionKind.PettingGestureRequired,
            resolver.Resolve(BodyRegionId.HeadAndHair, Now).Kind);
        Assert.Equal(BodyInteractionResolver.HeadPatAnimation, resolver.ResolvePetting().AnimationId);
    }

    [Fact]
    public void SensitiveRegionsShareEscalationWindowAndCooldown()
    {
        BodyInteractionResolver resolver = new();

        Assert.Equal(
            BodyInteractionResolver.GuiltyAnimation,
            resolver.Resolve(BodyRegionId.Chest, Now).AnimationId);
        Assert.Equal(
            BodyInteractionResolver.DarkAnimation,
            resolver.Resolve(BodyRegionId.LowerBodySensitiveArea, Now.AddSeconds(4)).AnimationId);
        Assert.Equal(
            BodyInteractionDecisionKind.SuppressedByCooldown,
            resolver.Resolve(BodyRegionId.Chest, Now.AddSeconds(13)).Kind);
        Assert.Equal(
            BodyInteractionResolver.GuiltyAnimation,
            resolver.Resolve(BodyRegionId.Chest, Now.AddSeconds(14)).AnimationId);
    }

    [Fact]
    public void ExpiredSensitiveRepeatWindowStartsAgainWithGuilty()
    {
        BodyInteractionResolver resolver = new();

        resolver.Resolve(BodyRegionId.Chest, Now);

        Assert.Equal(
            BodyInteractionResolver.GuiltyAnimation,
            resolver.Resolve(BodyRegionId.Chest, Now.AddSeconds(4).AddMilliseconds(1)).AnimationId);
    }

    [Fact]
    public void OrdinaryBodyUsesOnlyTheConfirmedHugAnimation()
    {
        BodyInteractionResolver resolver = new(_ => 0);

        Assert.Equal(
            BodyInteractionResolver.OrdinaryBodyAnimations[0],
            resolver.Resolve(BodyRegionId.OtherBody, Now).AnimationId);
        Assert.Equal(
            BodyInteractionResolver.OrdinaryBodyAnimations[0],
            resolver.Resolve(BodyRegionId.OtherBody, Now).AnimationId);
    }

    [Theory]
    [InlineData(BodyRegionId.LeftEye, BodyInteractionResolver.SoftHeartAnimation, true)]
    [InlineData(BodyRegionId.RightEye, BodyInteractionResolver.SoftHeartAnimation, false)]
    [InlineData(BodyRegionId.LeftHand, BodyInteractionResolver.HighFiveAnimation, false)]
    [InlineData(BodyRegionId.RightHand, BodyInteractionResolver.HighFiveAnimation, true)]
    [InlineData(BodyRegionId.LeftFoot, BodyInteractionResolver.OopsAnimation, true)]
    [InlineData(BodyRegionId.RightFoot, BodyInteractionResolver.OopsAnimation, false)]
    public void PairedRegionsUseTheConfirmedOrientation(
        BodyRegionId region,
        string expectedAnimation,
        bool expectedMirror)
    {
        BodyInteractionDecision result = new BodyInteractionResolver().Resolve(region, Now);

        Assert.Equal(expectedAnimation, result.AnimationId);
        Assert.Equal(expectedMirror, result.MirrorHorizontally);
    }

    [Theory]
    [InlineData(0.49, BodyInteractionResolver.LeftFaceAnimation)]
    [InlineData(0.50, BodyInteractionResolver.FaceAnimation)]
    [InlineData(0.72, BodyInteractionResolver.FaceAnimation)]
    public void FaceUsesPointerSideWithoutChangingTheDrawnFaceRegion(
        double normalizedPointerX,
        string expectedAnimation)
    {
        BodyInteractionDecision result = new BodyInteractionResolver().Resolve(
            BodyRegionId.Face,
            Now,
            normalizedPointerX);

        Assert.Equal(expectedAnimation, result.AnimationId);
        Assert.False(result.MirrorHorizontally);
    }

    [Theory]
    [InlineData(BodyRegionId.LeftEye, BodyRegionId.RightEye, BodyInteractionResolver.RepeatedEyeAnimation)]
    [InlineData(BodyRegionId.RightEye, BodyRegionId.LeftEye, BodyInteractionResolver.RepeatedEyeAnimation)]
    [InlineData(BodyRegionId.LeftFoot, BodyRegionId.RightFoot, BodyInteractionResolver.RepeatedFootAnimation)]
    [InlineData(BodyRegionId.RightFoot, BodyRegionId.LeftFoot, BodyInteractionResolver.RepeatedFootAnimation)]
    public void SecondClickAcrossAPairedRegionUsesItsEasterEgg(
        BodyRegionId firstRegion,
        BodyRegionId secondRegion,
        string expectedAnimation)
    {
        BodyInteractionResolver resolver = new();

        resolver.Resolve(firstRegion, Now);
        BodyInteractionDecision second = resolver.Resolve(secondRegion, Now.AddSeconds(4));

        Assert.Equal(expectedAnimation, second.AnimationId);
        Assert.False(second.MirrorHorizontally);
    }

    [Theory]
    [InlineData(BodyRegionId.LeftEye, BodyInteractionResolver.SoftHeartAnimation)]
    [InlineData(BodyRegionId.LeftFoot, BodyInteractionResolver.OopsAnimation)]
    public void PairedRegionClickAfterTheRepeatWindowStartsANewPair(
        BodyRegionId region,
        string expectedAnimation)
    {
        BodyInteractionResolver resolver = new();

        resolver.Resolve(region, Now);
        BodyInteractionDecision result = resolver.Resolve(region, Now.AddSeconds(4).AddMilliseconds(1));

        Assert.Equal(expectedAnimation, result.AnimationId);
    }

    [Theory]
    [InlineData(BodyRegionId.RightEye, BodyRegionId.LeftHand, BodyInteractionResolver.SoftHeartAnimation)]
    [InlineData(BodyRegionId.RightFoot, BodyRegionId.LeftHand, BodyInteractionResolver.OopsAnimation)]
    public void AClickOnAnotherBodyPartBreaksTheConsecutivePair(
        BodyRegionId pairedRegion,
        BodyRegionId interveningRegion,
        string expectedAnimation)
    {
        BodyInteractionResolver resolver = new();

        resolver.Resolve(pairedRegion, Now);
        resolver.Resolve(interveningRegion, Now.AddSeconds(1));
        BodyInteractionDecision result = resolver.Resolve(pairedRegion, Now.AddSeconds(2));

        Assert.Equal(expectedAnimation, result.AnimationId);
    }

    [Fact]
    public void PettingBreaksEyeAndFootConsecutivePairs()
    {
        BodyInteractionResolver resolver = new();
        resolver.Resolve(BodyRegionId.RightEye, Now);
        resolver.Resolve(BodyRegionId.RightFoot, Now.AddMilliseconds(1));

        resolver.ResolvePetting();

        Assert.Equal(
            BodyInteractionResolver.SoftHeartAnimation,
            resolver.Resolve(BodyRegionId.RightEye, Now.AddSeconds(1)).AnimationId);
        resolver.ResolvePetting();
        Assert.Equal(
            BodyInteractionResolver.OopsAnimation,
            resolver.Resolve(BodyRegionId.RightFoot, Now.AddSeconds(2)).AnimationId);
    }

    [Fact]
    public void SingleOrdinaryBodyAnimationDoesNotCallRandomSelector()
    {
        BodyInteractionResolver resolver = new(count => count);

        Assert.Equal(
            BodyInteractionResolver.OrdinaryBodyAnimations[0],
            resolver.Resolve(BodyRegionId.OtherBody, Now).AnimationId);
    }

    [Theory]
    [InlineData(BodyInteractionResolver.SoftHeartAnimation, 0.72)]
    [InlineData(BodyInteractionResolver.KissAnimation, 0.82)]
    [InlineData(BodyInteractionResolver.FaceAnimation, 0.80)]
    [InlineData(BodyInteractionResolver.LeftFaceAnimation, 0.80)]
    [InlineData(BodyInteractionResolver.HeadPatAnimation, 0.75)]
    [InlineData(BodyInteractionResolver.HighFiveAnimation, 0.68)]
    [InlineData(BodyInteractionResolver.GuiltyAnimation, 0.80)]
    [InlineData(BodyInteractionResolver.DarkAnimation, 0.90)]
    [InlineData(BodyInteractionResolver.OopsAnimation, 0.68)]
    [InlineData(BodyInteractionResolver.RepeatedEyeAnimation, 0.80)]
    [InlineData(BodyInteractionResolver.RepeatedFootAnimation, 0.80)]
    [InlineData(BodyInteractionResolver.HugAnimation, 0.80)]
    public void ClassicExpressionReactionsUseActionSpecificModerateRates(
        string animationId,
        double expectedRate)
    {
        Assert.Equal(expectedRate, BodyInteractionResolver.ResolvePlaybackRate(animationId));
    }

    [Fact]
    public void UnrelatedAnimationsKeepTheirAuthoredRate()
    {
        Assert.Equal(1.0, BodyInteractionResolver.ResolvePlaybackRate("unrelated-animation"));
    }
}
