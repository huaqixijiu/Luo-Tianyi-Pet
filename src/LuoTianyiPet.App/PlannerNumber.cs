using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
namespace LuoTianyiPet.App;
internal sealed class PlannerNumber : StackPanel
{
    internal TextBox Input {get;}
    internal int Value => int.TryParse(Input.Text,out int n)?n:-1;
    public PlannerNumber(string name,int value,int max)
    {
        Width=135;Margin=new Thickness(7,0,7,0);
        Input=new TextBox { Name=name,Text=value.ToString("00"),FontFamily=new System.Windows.Media.FontFamily("Segoe UI"),FontWeight=FontWeights.SemiBold,Foreground=PlannerTheme.Ink,Background=System.Windows.Media.Brushes.Transparent,FontSize=28,TextAlignment=TextAlignment.Center,Padding=new Thickness(4),BorderThickness=new Thickness(0) };
        Button up=new(){Content="⌃",Foreground=PlannerTheme.Accent,Background=System.Windows.Media.Brushes.Transparent,Padding=new Thickness(0),BorderThickness=new Thickness(0),Height=24};
        Button down=new(){Content="⌄",Foreground=PlannerTheme.Accent,Background=System.Windows.Media.Brushes.Transparent,Padding=new Thickness(0),BorderThickness=new Thickness(0),Height=24};
        void Step(int d) { Input.Text=Math.Max(0,Math.Min(max,Value+d)).ToString("00"); }
        up.Click+=(_,_)=>Step(1);down.Click+=(_,_)=>Step(-1);
        Children.Add(up);Children.Add(Input);Children.Add(down);
        PreviewMouseWheel+=(_,e)=>{Step(e.Delta>0?1:-1);e.Handled=true;};
        Input.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Up || e.Key==Key.Down){Step(e.Key==Key.Up?1:-1);e.Handled=true;}};
    }
}
