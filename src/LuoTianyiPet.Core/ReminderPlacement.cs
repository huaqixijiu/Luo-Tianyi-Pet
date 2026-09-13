namespace LuoTianyiPet.Core;
public static class ReminderPlacement
{
    public static DesktopRectangle Resolve(DesktopRectangle pet,DesktopRectangle work,double width,double height,double gap=10)
    {
        width=Math.Min(width,work.Width);height=Math.Min(height,work.Height);
        double x=Numeric.Clamp(pet.Left+(pet.Width-width)/2,work.Left,work.Right-width);
        double below=work.Bottom-pet.Bottom-gap,above=pet.Top-work.Top-gap;
        bool placeBelow=below>=height || below>=above;
        double available=Math.Max(0,placeBelow?below:above);
        height=Math.Min(height,available);
        double y=placeBelow?pet.Bottom+gap:pet.Top-gap-height;
        return new(x,Numeric.Clamp(y,work.Top,work.Bottom-height),width,height);
    }
}
