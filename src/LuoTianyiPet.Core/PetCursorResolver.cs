namespace LuoTianyiPet.Core;

public enum PetCursorKind
{
    Default,
    Interaction,
    HeadPat,
}

public static class PetCursorResolver
{
    public static PetCursorKind Resolve(
        bool isOpaquePetPixel,
        bool bodyRegionInteractionsEnabled,
        BodyRegionId? region)
    {
        if (!isOpaquePetPixel)
        {
            return PetCursorKind.Default;
        }

        return bodyRegionInteractionsEnabled && region == BodyRegionId.HeadAndHair
            ? PetCursorKind.HeadPat
            : PetCursorKind.Interaction;
    }
}
