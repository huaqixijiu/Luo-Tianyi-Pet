using System.IO;
using System.Windows;
using System.Windows.Controls;
using LuoTianyiPet.Core;
using CheckBox=System.Windows.Controls.CheckBox;
using ComboBox=System.Windows.Controls.ComboBox;
using Button=System.Windows.Controls.Button;
namespace LuoTianyiPet.App;
internal sealed class ReminderSettingsWindow : Window
{
    public event Action<ReminderPreferences>? TestRequested;
    public ReminderSettingsWindow(ReminderService service)
    {
        Title="提醒设置";Width=410;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;FontSize=14;FontFamily=new System.Windows.Media.FontFamily("Microsoft YaHei UI");PlannerTheme.Apply(this);
        StackPanel p=new(){Margin=new Thickness(24)};Content=p;
        p.Children.Add(new TextBlock{Text="提醒设置",FontSize=23,Margin=new Thickness(0,0,0,18)});
        var animation=new CheckBox{Content="播放桌宠提醒动画",IsChecked=service.Book.Preferences.Animation,Margin=new Thickness(0,8,0,8)};
        var sound=new CheckBox{Content="播放提醒声音",IsChecked=service.Book.Preferences.Sound,Margin=new Thickness(0,8,0,8)};
        var tone=new ComboBox{ItemsSource=new[]{"轻提示","双音"},SelectedIndex=service.Book.Preferences.Tone=="双音"?1:0,Margin=new Thickness(0,8,0,8)};
        var volume=new Slider{Minimum=0,Maximum=1,Value=service.Book.Preferences.Volume,Margin=new Thickness(0,8,0,8)};
        p.Children.Add(animation);p.Children.Add(sound);p.Children.Add(new TextBlock{Text="提醒音"});p.Children.Add(tone);p.Children.Add(new TextBlock{Text="音量"});p.Children.Add(volume);
        ReminderPreferences Values()=>new(){Animation=animation.IsChecked==true,Sound=sound.IsChecked==true,Tone=tone.SelectedIndex==1?"双音":"轻提示",Volume=volume.Value};
        var test=new Button{Content="测试提醒",Margin=new Thickness(0,12,0,8)};test.Click+=(_,_)=>{ReminderAudio.Play(Values());TestRequested?.Invoke(Values());test.Content="测试提醒 · 时间到了";};p.Children.Add(test);
        p.Children.Add(new TextBlock{Text="日历、闹钟和倒计时共用此设置。关闭声音或动画，仍会显示提醒。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,12)});
        var save=new Button{Content="保存",Background=PlannerTheme.Accent,Foreground=System.Windows.Media.Brushes.White};save.Click+=async(_,_)=>{try{await service.ChangeAsync(b=>b.Preferences=Values());Close();}catch{save.Content="保存失败，请重试";}};p.Children.Add(save);
    }
}
internal static class ReminderAudio
{
    private static System.Media.SoundPlayer? _player;
    private static MemoryStream? _stream;
    public static void Play(ReminderPreferences p)
    {
        if(!p.Sound||p.Volume<=0)return;
        try
        {
            _player?.Stop();_player?.Dispose();_stream?.Dispose();
            int rate=22050,count=rate/2;_stream=new MemoryStream();using(var w=new BinaryWriter(_stream,System.Text.Encoding.UTF8,true))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);
                for(int n=0;n<count;n++){double t=(double)n/rate;double frequency=p.Tone=="双音"&&n>count/2?880:660;double envelope=Math.Sin(Math.PI*n/count);w.Write((short)(Math.Sin(2*Math.PI*frequency*t)*envelope*6000*p.Volume));}
            }
            _stream.Position=0;_player=new System.Media.SoundPlayer(_stream);_player.Play();
        }catch{ }
    }
}
