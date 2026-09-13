using System.Windows;
using Brush = System.Windows.Media.Brush;
using System.Windows.Markup;
using System.Windows.Media;
namespace LuoTianyiPet.App;
internal static class PlannerTheme
{
    public static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0, 157, 211));
    public static readonly Brush Line = new SolidColorBrush(Color.FromRgb(222, 237, 248));
    public static readonly Brush Soft = new SolidColorBrush(Color.FromRgb(234, 248, 255));
    public static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(27, 43, 88));
    public static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(118, 139, 167));
    public static FrameworkElement Bell() => new System.Windows.Shapes.Path { Width=14,Height=14,Margin=new Thickness(0,0,4,0),Stretch=Stretch.Uniform,Stroke=Accent,StrokeThickness=1.5,Data=Geometry.Parse("M 3,11 L 4,9 L 4,6 C 4,2 10,2 10,6 L 10,9 L 11,11 Z M 6,13 Q 7,15 8,13 M 7,1 L 7,2") };
    public static void Apply(Window window)
    {
        window.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse("""
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
 <Style TargetType="Button">
  <Setter Property="Background" Value="White"/><Setter Property="Foreground" Value="#1B2B58"/><Setter Property="BorderBrush" Value="#DEEDF8"/><Setter Property="BorderThickness" Value="1"/><Setter Property="Padding" Value="10,7"/><Setter Property="Cursor" Value="Hand"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button"><Border x:Name="B" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="8" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="B" Property="Opacity" Value="0.78"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter TargetName="B" Property="Opacity" Value="0.4"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="B" Property="BorderBrush" Value="#009DD3"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType="TextBox">
  <Setter Property="Padding" Value="12,10"/><Setter Property="FontSize" Value="15"/><Setter Property="BorderBrush" Value="#CDDEE6"/><Setter Property="Background" Value="White"/><Setter Property="BorderThickness" Value="1"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TextBox"><Border x:Name="Frame" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="7"><ScrollViewer x:Name="PART_ContentHost" /></Border><ControlTemplate.Triggers><Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="Frame" Property="BorderBrush" Value="#009DD3"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType="ComboBox">
  <Setter Property="FontSize" Value="15"/><Setter Property="MinHeight" Value="42"/><Setter Property="Background" Value="White"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox"><Grid><ToggleButton Focusable="False" IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"><ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border CornerRadius="7" Background="White" BorderBrush="#CDDEE6" BorderThickness="1"><TextBlock Text="⌄" HorizontalAlignment="Right" Margin="0,0,14,0" VerticalAlignment="Center" Foreground="#345569"/></Border></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter Margin="12,8,34,8" VerticalAlignment="Center" IsHitTestVisible="False" Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"/><Popup x:Name="PART_Popup" Placement="Bottom" IsOpen="{TemplateBinding IsDropDownOpen}" AllowsTransparency="True" Focusable="False" PopupAnimation="Fade"><Border Background="White" BorderBrush="#CDDEE6" BorderThickness="1" CornerRadius="7" MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}" Padding="5"><ScrollViewer MaxHeight="270"><ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained"/></ScrollViewer></Border></Popup></Grid></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType="ComboBoxItem"><Setter Property="Padding" Value="10,8"/><Setter Property="HorizontalContentAlignment" Value="Stretch"/></Style>
 <Style TargetType="CheckBox"><Setter Property="VerticalContentAlignment" Value="Center"/><Setter Property="Padding" Value="4"/><Setter Property="Foreground" Value="#345569"/>
 <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox"><StackPanel Orientation="Horizontal"><Border x:Name="Box" Width="16" Height="16" CornerRadius="4" BorderBrush="#ADCAD3" BorderThickness="1" Background="White" VerticalAlignment="Center"><TextBlock x:Name="Check" Text="✓" Foreground="White" FontSize="12" HorizontalAlignment="Center" VerticalAlignment="Center" Visibility="Collapsed"/></Border><ContentPresenter Margin="7,0,0,0" VerticalAlignment="Center"/></StackPanel><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Box" Property="Background" Value="#009DD3"/><Setter TargetName="Check" Property="Visibility" Value="Visible"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Box" Property="BorderBrush" Value="#1B2B58"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style x:Key="PlannerSwitch" TargetType="CheckBox"><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox"><StackPanel Orientation="Horizontal"><Border x:Name="Track" Width="38" Height="22" CornerRadius="11" Background="#B7C9CE"><Ellipse x:Name="Knob" Width="16" Height="16" Fill="White" HorizontalAlignment="Left" Margin="3"/></Border><ContentPresenter Margin="8,0,0,0" VerticalAlignment="Center"/></StackPanel><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Track" Property="Background" Value="#009DD3"/><Setter TargetName="Knob" Property="HorizontalAlignment" Value="Right"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Track" Property="BorderBrush" Value="#1B2B58"/><Setter TargetName="Track" Property="BorderThickness" Value="1"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>
"""));
    }
}
