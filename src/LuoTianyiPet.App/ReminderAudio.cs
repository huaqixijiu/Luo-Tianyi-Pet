using System.IO;
using System.Windows.Media;
using LuoTianyiPet.Core;
namespace LuoTianyiPet.App;

internal static class ReminderAudio
{
    private static MediaPlayer? _player;
    internal static bool IsPlaying { get; private set; }
    internal static string SongPath => Path.Combine(AppContext.BaseDirectory,"assets","audio","reminder-song.mp3");
    public static void Play(ReminderPreferences preferences)
    {
        Stop();
        if(!preferences.Sound || preferences.Volume<=0 || !File.Exists(SongPath))return;
        try
        {
            var player=new MediaPlayer{Volume=preferences.Volume};_player=player;
            player.MediaEnded+=(_,_)=>{if(ReferenceEquals(_player,player))Stop();};
            player.MediaFailed+=(_,_)=>{if(ReferenceEquals(_player,player))Stop();};
            player.Open(new Uri(SongPath));player.Play();IsPlaying=true;
        }
        catch { Stop(); }
    }
    public static void Stop(){_player?.Stop();_player?.Close();_player=null;IsPlaying=false;}
}
