using LuoTianyiPet.Platform.Windows;
using LuoTianyiPet.Core;
var snapshot=WeChatSessionReader.TryRead();
Console.WriteLine($"Snapshot={snapshot!=null}; Minimized={snapshot?.Minimized}; Foreground={snapshot?.Foreground}; ParsedRows={snapshot?.Rows.Count}; UnreadRows={snapshot?.Rows.Count(r=>r.HasUnreadMarker)}; MutedRows={snapshot?.Rows.Count(r=>r.Muted)}");
using var source=new WindowsWeChatSessionNotificationSource();
source.NotificationReceived+=(_,e)=>Console.WriteLine($"Event Provider={e.Provider}; TitleLength={e.Notification.ConversationDisplayName?.Length??0}; PreviewLength={e.Notification.MessagePreview?.Length??0}; HasKey={e.Notification.NotificationKey!=null}");
source.Start();
for(int i=0;i<80;i++)Thread.Sleep(1500);
source.Stop();Console.WriteLine("Finished");
