using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class PetCursorResolverTests
{
    [Fact]
    public void TransparentPixelUsesDefaultCursor()
    {
        PetCursorKind result = PetCursorResolver.Resolve(
            isOpaquePetPixel: false,
            bodyRegionInteractionsEnabled: true,
            BodyRegionId.HeadAndHair);

        Assert.Equal(PetCursorKind.Default, result);
    }

    [Fact]
    public void CompactCharacterUsesGeneralInteractionCursor()
    {
        PetCursorKind result = PetCursorResolver.Resolve(
            isOpaquePetPixel: true,
            bodyRegionInteractionsEnabled: false,
            BodyRegionId.HeadAndHair);

        Assert.Equal(PetCursorKind.Interaction, result);
    }

    [Fact]
    public void FullBodyHeadRegionUsesHeadPatCursor()
    {
        PetCursorKind result = PetCursorResolver.Resolve(
            isOpaquePetPixel: true,
            bodyRegionInteractionsEnabled: true,
            BodyRegionId.HeadAndHair);

        Assert.Equal(PetCursorKind.HeadPat, result);
    }

    [Fact]
    public void FullBodyNonHeadRegionUsesGeneralInteractionCursor()
    {
        PetCursorKind result = PetCursorResolver.Resolve(
            isOpaquePetPixel: true,
            bodyRegionInteractionsEnabled: true,
            BodyRegionId.LeftHand);

        Assert.Equal(PetCursorKind.Interaction, result);
    }
}
