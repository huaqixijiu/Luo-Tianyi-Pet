using LuoTianyiPet.Animation;

namespace LuoTianyiPet.Animation.Tests;

public sealed class AnimationDescriptorTests
{
    [Fact]
    public void Validate_AcceptsAValidDescriptor()
    {
        AnimationDescriptor descriptor = new("idle", "assets/idle.webp", 0, 0.5);

        Assert.Empty(descriptor.Validate());
    }

    [Fact]
    public void Validate_RejectsInvalidPlaybackValues()
    {
        AnimationDescriptor descriptor = new("", "", -1, double.NaN);

        Assert.Equal(4, descriptor.Validate().Count);
    }
}
