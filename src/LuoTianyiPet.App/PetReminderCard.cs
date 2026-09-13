using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Button=System.Windows.Controls.Button;

namespace LuoTianyiPet.App;

// A single owned, non-activating surface attached to the pet. The same border,
// header and content viewport survive collapsed/expanded transitions.
internal sealed class PetReminderCard : Window
{
    internal Border Surface { get; }
    internal Button Header { get; }
    private readonly TextBlock _summary=new(){TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center};
    private readonly TextBlock _arrow=new(){Text="⌄",FontSize=20,Foreground=PlannerTheme.Muted,VerticalAlignment=VerticalAlignment.Center};
    private readonly ScrollViewer _viewport=new(){HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,ClipToBounds=true};
    private bool _quick;
    internal double TargetHeight { get; private set; }
    internal event Action? ToggleRequested;
    internal event Action? GeometryChanged;

    internal PetReminderCard(Window owner)
    {
        Owner=owner;Title="天依快速提醒";ShowInTaskbar=false;ShowActivated=false;Topmost=true;
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;
        Background=Brushes.Transparent;SizeToContent=SizeToContent.Height;Width=356;
        FontFamily=new System.Windows.Media.FontFamily("Microsoft YaHei UI");FontSize=14;Foreground=PlannerTheme.Ink;
        PlannerTheme.Apply(this);
        DockPanel headerContent=new(){LastChildFill=true};
        DockPanel.SetDock(_arrow,Dock.Right);headerContent.Children.Add(_arrow);
        var bell=PlannerTheme.Bell();bell.Margin=new Thickness(0,0,10,0);DockPanel.SetDock(bell,Dock.Left);headerContent.Children.Add(bell);headerContent.Children.Add(_summary);
        Header=new(){Name="ToggleQuickReminder",Content=headerContent,Height=48,Padding=new Thickness(16,6,16,6),BorderThickness=new Thickness(0),Background=PlannerTheme.Soft,HorizontalContentAlignment=System.Windows.HorizontalAlignment.Stretch};
        Header.Click+=(_,_)=>ToggleRequested?.Invoke();
        DockPanel content=new();DockPanel.SetDock(Header,Dock.Top);content.Children.Add(Header);content.Children.Add(_viewport);
        content.SizeChanged+=(_,_)=>content.Clip=new RectangleGeometry(new Rect(0,0,content.ActualWidth,content.ActualHeight),15,15);
        Surface=new(){CornerRadius=new CornerRadius(16),Background=new SolidColorBrush(Color.FromRgb(248,252,255)),BorderBrush=Brushes.White,BorderThickness=new Thickness(1),Child=content,ClipToBounds=true,
            Effect=new DropShadowEffect{BlurRadius=12,ShadowDepth=3,Opacity=.16,Color=Color.FromRgb(41,90,135)}};
        Content=new Border{Padding=new Thickness(8),Child=Surface};
        SizeChanged+=(_,_)=>GeometryChanged?.Invoke();
        DpiChanged+=(_,_)=>GeometryChanged?.Invoke();
    }

    internal void Present(string summary,UIElement body,bool quick,bool expanded)
    {
        bool initial=!IsVisible || _quick!=quick;
        _quick=quick;_summary.Text=summary;Header.Visibility=quick?Visibility.Visible:Visibility.Collapsed;
        _viewport.Content=body;SetExpanded(expanded,!initial);
    }

    internal void SetExpanded(bool expanded,bool animate=true)
    {
        _arrow.Text=expanded?"⌃":"⌄";
        Header.ToolTip=expanded?"收起提醒":"展开提醒";
        _viewport.IsHitTestVisible=!_quick||expanded;
        var body=(UIElement)_viewport.Content;
        body.Measure(new System.Windows.Size(Math.Max(80,Width-18),double.PositiveInfinity));
        double desired=Math.Min(340,body.DesiredSize.Height);
        double height=_quick&&!expanded?0:desired;
        double from=_viewport.ActualHeight;
        _viewport.BeginAnimation(HeightProperty,null);_viewport.Height=height;
        TargetHeight=height+(_quick?48:0)+18;
        if(animate&&Math.Abs(from-height)>1)
            _viewport.BeginAnimation(HeightProperty,new DoubleAnimation(from,height,TimeSpan.FromMilliseconds(180)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop});
        GeometryChanged?.Invoke();
    }

    internal void LimitHeight(double available)
    {
        MaxHeight=Math.Max(18,available);
        // Constrain the scroll viewport itself, not only its transparent host;
        // otherwise a large pet can clip the lower buttons without a scrollbar.
        _viewport.MaxHeight=Math.Max(0,MaxHeight-(_quick?48:0)-18);
    }
}
