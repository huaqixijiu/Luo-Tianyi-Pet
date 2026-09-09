using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class BodyInteractionResolverTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.FromHours(8));

    [Theory]
    [InlineData(BodyRegionId.LeftEye, BodyInteractionResolver.SoftHeartAnimation)]
    [InlineData(BodyRegionId.RightEye, BodyInteractionResolver.SoftHeartAnimation)]
    [InlineData(BodyRegionId.Mouth, BodyInteractionResolver.KissAnimation)]
    [InlineData(BodyRegionId.Face, BodyInteractionResolver.FaceAnimation)]
    [InlineData(BodyRegionId.LeftHand, BodyInteractionResolver.HighFiveAnimation)]
    [InlineData(BodyRegionId.RightHand, BodyInteractionResolver.HighFiveAnimation)]
    [InlineData(BodyRegionId.LeftFoot, BodyInteractionResolver.OopsAnimation)]
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
    [InlineData(BodyInteractionResolver.HeadPatAnimation, 0.75)]
    [InlineData(BodyInteractionResolver.HighFiveAnimation, 0.68)]
    [InlineData(BodyInteractionResolver.GuiltyAnimation, 0.80)]
    [InlineData(BodyInteractionResolver.DarkAnimation, 0.90)]
    [InlineData(BodyInteractionResolver.OopsAnimation, 0.68)]
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
