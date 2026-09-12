using System.IO;
using System.Reflection;
using System.Windows.Threading;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

internal static class Program
{
    private static readonly string Output=Path.Combine(AppContext.BaseDirectory,"result.txt");
    [STAThread] private static void Main()
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        File.WriteAllText(Output,"Production QQ preview probe; field lengths and booleans only.\n");
        Dispatcher.CurrentDispatcher.BeginInvoke(new Action(async()=>
        {
            try { await Run(); }
            catch(Exception e) { Log($"FAIL Type={e.GetType().Name}; HResult={e.HResult:X8}"); }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }));
        Dispatcher.Run();
    }
    private static void Log(string text)=>File.AppendAllText(Output,text+"\n");
    private static async Task Run()
    {
        var matcher=new MessageProviderMatcher(new());
        using var source=new WindowsMessageNotificationSource(matcher);
        Log("Access="+source.GetAccessStatus());
        if(source.GetAccessStatus()!=MessageNotificationAccessStatus.Allowed)return;
        var notifications=await UserNotificationListener.Current.GetNotificationsAsync(NotificationKinds.Toast);
        var latest=notifications.Where(n=>matcher.Identify(n.AppInfo.AppUserModelId,n.AppInfo.DisplayInfo.DisplayName)==MessageProvider.Qq)
            .OrderByDescending(n=>n.CreationTime).FirstOrDefault();
        Log($"QqSnapshotAvailable={latest!=null}");
        if(latest==null)return;
        string stage="baseline";
        source.NotificationReceived+=(_,e)=>Log($"Stage={stage}; TitleLength={e.Notification.ConversationDisplayName?.Length??0}; PreviewLength={e.Notification.MessagePreview?.Length??0}; HasIdentity={e.Notification.NotificationKey!=null}");
        source.Start();
        var method=typeof(WindowsMessageNotificationSource).GetMethod("ProcessNotificationAsync",BindingFlags.NonPublic|BindingFlags.Instance)!;
        stage="disabled";source.SetQqDetailsEnabled(false);
        await (Task)method.Invoke(source,new object[]{latest})!;
        stage="enabled";source.SetQqDetailsEnabled(true);
        await (Task)method.Invoke(source,new object[]{latest})!;
        stage="live";
        for(int i=0;i<120 && !File.Exists(Path.Combine(AppContext.BaseDirectory,"stop"));i++)await Task.Delay(1000);
        source.Stop();Log("Finished");
    }
}
