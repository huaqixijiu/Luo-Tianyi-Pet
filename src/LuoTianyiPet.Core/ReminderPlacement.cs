namespace LuoTianyiPet.Core;
public static class ReminderPlacement
{
    public static DesktopRectangle Resolve(DesktopRectangle pet,DesktopRectangle work,double width,double height)
    {
        width=Math.Min(width,work.Width);height=Math.Min(height,work.Height);
        var candidates=new[]{(pet.Right+10,pet.Top-height-10),(pet.Left-width-10,pet.Top-height-10),(pet.Right+10,pet.Bottom+10),(pet.Left-width-10,pet.Bottom+10),(pet.Right+10,pet.Top),(pet.Left-width-10,pet.Top),(pet.Left,pet.Top-height-10),(pet.Left,pet.Bottom+10)};
        foreach(var (x,y) in candidates)if(x>=work.Left&&y>=work.Top&&x+width<=work.Right&&y+height<=work.Bottom)return new(x,y,width,height);
        return new(Numeric.Clamp(pet.Right+10,work.Left,work.Right-width),Numeric.Clamp(pet.Top-height-10,work.Top,work.Bottom-height),width,height);
    }
}
