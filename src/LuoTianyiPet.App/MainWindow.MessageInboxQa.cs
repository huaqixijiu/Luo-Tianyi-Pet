using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuoTianyiPet.Core;
using Size = System.Windows.Size;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private async Task RunMessageInboxQaAsync()
    {
        string directory=Path.Combine(AppContext.BaseDirectory,"MessageInboxQa"); Directory.CreateDirectory(directory);
        List<string> checks=[]; int serial=0;
        void Check(bool ok,string text) { if(!ok)throw new InvalidOperationException(text);checks.Add("PASS "+text); }
        MessageNotificationSummary Sample(MessageProvider p,string name="小雨",string? preview=null) => new(p,DateTimeOffset.Now,
            name,MessagePreview:preview??"周末见吗？",NotificationKey:$"qa:{++serial}");
        try
        {
            await Task.Delay(1200);
            _messageNotificationStatusTimer.Stop(); _idleSceneTimer.Stop(); _timeSceneTimer.Stop(); _musicDetectionTimer.Stop();
            _stateMachine.CancelActiveReaction(); _stateMachine.SetContinuousState(PetContinuousState.Idle);
            _stateMachine.SetDisplayMode(PetDisplayMode.FullBodyInteractive);
            _settings=_settings with { Notifications=_settings.Notifications with {EnableMessageReminders=true,EnableQqDetailedReminders=true,EnableWeChatDetailedReminders=true} };
            _inboxSafe=true; RefreshMessageInbox(); _inboxWindow!.AutomationMode=true;
            foreach(string style in new[]{"full-body-long-hair","full-body-crystal-dress","full-body-classic-cat-ears"})
            {
                ApplyAppearancePreferences(_settings.Appearance with {FullBodyStyle=style,DisplayScalePercent=100},false);
                await Task.Delay(400); _stateMachine.SetContinuousState(PetContinuousState.Idle);
                PlayResolvedContinuousAnimation(); await Task.Delay(180);
                Check(_animationPlayer?.CurrentAnimationId==AppearanceOptionIds.ResolveFullBodyAnimation(style),$"{style}: correct selected idle artwork");
                foreach(string side in new[]{"left","right"})
                {
                    List<BitmapSource> previews=[];
                    for(int state=1;state<=8;state++)
                    {
                        _messageInbox.Clear(MessageProvider.WeChat,DateTimeOffset.MinValue); _messageInbox.Clear(MessageProvider.Qq,DateTimeOffset.MinValue);
                        _inboxWindow.Interaction.Close(); _inboxSide=null;
                        var work=GetCurrentWorkArea();
                        Left=work.Left+(side=="left" ? 70 : work.Width-Width-70); Top=work.Top+(work.Height-Height)/2;
                        string? animation=_animationPlayer?.CurrentAnimationId; var plan=_stateMachine.Resolve(DateTimeOffset.Now).Source;
                        if(state<=5) { await BeginMessageNotificationAsync(Sample(MessageProvider.WeChat)); await BeginMessageNotificationAsync(Sample(MessageProvider.WeChat)); await BeginMessageNotificationAsync(Sample(MessageProvider.WeChat,"林同学","文件发你了")); }
                        if(state<=3 || state==6 || state==7)
                        { await BeginMessageNotificationAsync(Sample(MessageProvider.Qq,"小洛","一起听歌吗？")); await BeginMessageNotificationAsync(Sample(MessageProvider.Qq,"小洛")); await BeginMessageNotificationAsync(Sample(MessageProvider.Qq,"阿言","我到啦")); }
                        if(state==2 || state==5) _inboxWindow.Interaction.Click(MessageProvider.WeChat);
                        if(state==3 || state==7) _inboxWindow.Interaction.Click(MessageProvider.Qq);
                        nint foreground=NotificationQaGetForegroundWindow();RefreshMessageInbox();
                        Check(NotificationQaGetForegroundWindow()==foreground,$"{style}/{side}/{state}: passive rail refresh preserves foreground focus");
                        await Task.Delay(70);
                        Check(_animationPlayer?.CurrentAnimationId==animation && _stateMachine.Resolve(DateTimeOffset.Now).Source==plan,
                            $"{style}/{side}/{state}: incoming reminders preserve current animation");
                        Check(_inboxWindow.IsVisible==(state!=8),$"{style}/{side}/{state}: zero reminders hide whole group");
                        if(state!=8) Check(_inboxSide==(side=="left"?MessageRailSide.Right:MessageRailSide.Left),$"{style}/{side}/{state}: rail faces available side");
                        Check(_messageInbox.Total(MessageProvider.WeChat)==_messageInbox.Rows(MessageProvider.WeChat).Sum(r=>(long)r.Count) &&
                            _messageInbox.Total(MessageProvider.Qq)==_messageInbox.Rows(MessageProvider.Qq).Sum(r=>(long)r.Count),$"{style}/{side}/{state}: badge totals equal row totals");
                        var image=CaptureInboxQa(directory,$"{style}-{side}-{state}.png");previews.Add(image);
                    }
                    SaveInboxSheet(directory,$"{style}-{side}-overview.png",previews);
                }
            }
            _messageInbox.Clear(MessageProvider.WeChat,DateTimeOffset.MinValue); _messageInbox.Clear(MessageProvider.Qq,DateTimeOffset.MinValue);
            for(int i=0;i<105;i++) _messageInbox.Add(Sample(MessageProvider.WeChat,"很长很长很长很长的联系人名称用于检查省略号","很长很长的消息摘要，必须在单行末尾省略，不能撑宽面板或影响滚动。"));
            for(int i=0;i<6;i++) _messageInbox.Add(Sample(MessageProvider.WeChat,$"测试会话{i}"));
            _messageInbox.Add(Sample(MessageProvider.Qq,"QQ好友")); _inboxWindow.Interaction.Close(); RefreshMessageInbox();
            Point icon=_inboxWindow.IconCenter(MessageProvider.WeChat); var now=DateTimeOffset.Now;
            _inboxWindow.ProcessPointer(icon,false,now); _inboxWindow.ProcessPointer(icon,false,now.AddMilliseconds(299));
            Check(_inboxWindow.Interaction.OpenProvider is null,"Hover waits 300 ms");
            _inboxWindow.ProcessPointer(icon,false,now.AddMilliseconds(301));
            Check(_inboxWindow.Interaction.OpenProvider==MessageProvider.WeChat,"Hover opens WeChat");
            var panel=_inboxWindow.PanelRectangle; icon=_inboxWindow.IconCenter(MessageProvider.WeChat);
            var gap=new Point((icon.X+(panel.Right<icon.X?panel.Right:panel.Left))/2,Numeric.Clamp(icon.Y,panel.Top,panel.Bottom));
            _inboxWindow.ProcessPointer(gap,false,now.AddMilliseconds(350));
            _inboxWindow.ProcessPointer(new(panel.Left+30,panel.Top+25),false,now.AddMilliseconds(950));
            Check(_inboxWindow.Interaction.OpenProvider==MessageProvider.WeChat,"Icon to bridge to list stays continuously open");
            Check(_inboxWindow.QaNativeHitTest(icon) && _inboxWindow.QaNativeHitTest(gap) &&
                !_inboxWindow.QaNativeHitTest(new(-5,-5)),"Native HWND hit testing accepts icons and bridge and passes empty space through");
            _inboxWindow.ScrollTo(70); await Task.Delay(80); double offset=_inboxWindow.ScrollOffset;
            string[] order=_inboxWindow.VisibleOrder.ToArray();
            _messageInbox.Add(Sample(MessageProvider.WeChat,"测试会话0","更新后的摘要")); RefreshMessageInbox(); await Task.Delay(70);
            Check(order.SequenceEqual(_inboxWindow.VisibleOrder) && Math.Abs(_inboxWindow.ScrollOffset-offset)<1,"Reading freezes row order and scroll position while content updates");
            Check(MessageInbox.Badge(_messageInbox.Total(MessageProvider.WeChat))=="99+","App badge supports 99+");
            CaptureInboxQa(directory,"long-scroll-99.png");
            _inboxWindow.ProcessPointer(new(-100,-100),false,now.AddSeconds(2));
            _inboxWindow.ProcessPointer(new(-100,-100),false,now.AddMilliseconds(2499)); Check(_inboxWindow.Interaction.OpenProvider is not null,"Leave grace period is 500 ms");
            _inboxWindow.ProcessPointer(new(-100,-100),false,now.AddMilliseconds(2501)); Check(_inboxWindow.Interaction.OpenProvider is null,"Leaving closes without clearing reminders");
            _inboxWindow.Interaction.Click(MessageProvider.WeChat);RefreshMessageInbox();
            _inboxWindow.ProcessPointer(new(-100,-100),true,now.AddSeconds(4));Check(_inboxWindow.Interaction.OpenProvider is null,"Outside click closes pinned list");
            Check(_messageInbox.Total(MessageProvider.WeChat)>100,"Collapsing preserves pending counts");
            _inboxWindow.QaClickIcon(MessageProvider.WeChat);Check(_inboxWindow.Interaction.Pinned,"Actual icon mouse route pins the list");
            var ignored=_messageInbox.Rows(MessageProvider.WeChat)[0];long totalBefore=_messageInbox.Total(MessageProvider.WeChat);
            _inboxWindow.QaIgnoreConversation(ignored.Key);
            Check(_messageInbox.Total(MessageProvider.WeChat)==totalBefore-ignored.Count && !_messageInbox.Add(ignored.Latest),"Attached conversation menu ignores locally and rejects replay");
            _inboxWindow.QaClickIcon(MessageProvider.WeChat);Check(_inboxWindow.Interaction.OpenProvider is null,"Second icon click closes pinned list");
            _inboxWindow.QaClearApplication(MessageProvider.Qq);Check(_messageInbox.Total(MessageProvider.Qq)==0 && _messageInbox.Total(MessageProvider.WeChat)>0,"Attached app menu clears only its own pending reminders");
            var beforeSide=_inboxSide; _isWindowDragging=true;
            Left=GetCurrentWorkArea().Left+50;PositionMessageInbox();Check(_inboxSide==beforeSide,"Dragging freezes rail side");
            _isWindowDragging=false;PositionMessageInbox();Check(_inboxSide==MessageRailSide.Right,"Drag end selects new side");
            foreach(int scale in new[]{50,150,200})
            {
                SetDisplayScalePercent(scale,false);PlayResolvedContinuousAnimation();await Task.Delay(150);
                foreach(bool bottom in new[]{false,true})
                {
                    Top=bottom?GetCurrentWorkArea().Bottom-Height:GetCurrentWorkArea().Top;
                    var petBounds=GetPetImageAlphaBoundsInWindow();
                    Point actualEdge=PointToScreen(new(petBounds.Left,bottom?petBounds.Bottom:petBounds.Top));
                    double dpi=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11??1;
                    var physicalWork=_windowWorkAreaProvider.GetForWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
                    Top+=((bottom?physicalWork.Bottom:physicalWork.Top)-actualEdge.Y)/dpi;
                    _inboxWindow.Interaction.Close();_inboxWindow.Interaction.Click(MessageProvider.WeChat);RefreshMessageInbox();
                    var pr=_inboxWindow.PanelRectangle;
                    Point panelTop=_inboxWindow.PointToScreen(pr.TopLeft), panelBottom=_inboxWindow.PointToScreen(pr.BottomRight);
                    Check(panelTop.X>=physicalWork.Left && panelBottom.X<=physicalWork.Right+1 &&
                        panelTop.Y>=physicalWork.Top && panelBottom.Y<=physicalWork.Bottom+1,$"Scale {scale}, edge {bottom}: full panel stays inside physical work area");
                    CaptureInboxQa(directory,$"edge-{scale}-{bottom}.png");
                }
            }
            Check(!IsInboxDisplaySafe(new(false,null,false)) && !IsInboxDisplaySafe(new(true,"other",true)),"Unknown foreground and fullscreen suppress interactive rail");
            _inboxSafe=false;PositionMessageInbox();Check(!_inboxWindow.IsVisible,"Safety suspension hides rail and panel");
            _inboxSafe=true;SetDisplayScalePercent(100,false);Top=GetCurrentWorkArea().Top+(GetCurrentWorkArea().Height-Height)/2;
            _stateMachine.SetMusicAnimation("resonance-enjoy-music");_stateMachine.SetContinuousState(PetContinuousState.MusicPlaying);
            PlayResolvedContinuousAnimation();await Task.Delay(150);string? music=_animationPlayer?.CurrentAnimationId;var mediaSettings=_settings.Media;
            await BeginMessageNotificationAsync(Sample(MessageProvider.Qq,"音乐中收到的测试提醒"));
            Check(_animationPlayer?.CurrentAnimationId==music && _stateMachine.VisualState.ContinuousState==PetContinuousState.MusicPlaying && _settings.Media==mediaSettings,
                "Message arrival preserves music animation and media settings without media input");
            _inboxWindow.Interaction.Close();RefreshMessageInbox();CaptureInboxQa(directory,"music-reminder.png");
            File.WriteAllLines(Path.Combine(directory,"result.txt"),checks);
            if(Environment.GetCommandLineArgs().Contains("--qa-inbox-interactive"))
            {
                SetDisplayScalePercent(100,false);_stateMachine.SetContinuousState(PetContinuousState.Idle);PlayResolvedContinuousAnimation();
                Left=GetCurrentWorkArea().Left+180;Top=GetCurrentWorkArea().Top+(GetCurrentWorkArea().Height-Height)/2;
                Topmost=true;_inboxSafe=true;_inboxSide=null;_inboxWindow.AutomationMode=false;RefreshMessageInbox();
                _inboxWindow.ExposeAutomationWindow();
            }
            else Close();
        }
        catch(Exception e){checks.Add("FAIL "+e);File.WriteAllLines(Path.Combine(directory,"result.txt"),checks);Application.Current.Shutdown(1);}
    }

    private BitmapSource CaptureInboxQa(string directory,string name)
    {
        UpdateLayout();_inboxWindow!.UpdateLayout();
        var alpha=GetPetImageAlphaBoundsInWindow(); var a=PointToScreen(new(alpha.Left,alpha.Top));var b=PointToScreen(new(alpha.Right,alpha.Bottom));
        double scale=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11??1;
        var origin=PointToScreen(new());
        Rect bounds=new(a,b);
        Point railOrigin=default;
        if(_inboxWindow.IsVisible){railOrigin=_inboxWindow.PointToScreen(new());bounds.Union(new Rect(railOrigin,new Size(_inboxWindow.ActualWidth*scale,_inboxWindow.ActualHeight*scale)));}
        bounds.Inflate(12,12);
        DrawingVisual drawing=new();
        using(var dc=drawing.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xED,0xF5,0xFC)),null,new Rect(0,0,bounds.Width,bounds.Height));
            dc.PushClip(new RectangleGeometry(new Rect(a.X-bounds.Left,a.Y-bounds.Top,b.X-a.X,b.Y-a.Y)));
            dc.DrawImage(RenderWindow(this),new Rect(origin.X-bounds.Left,origin.Y-bounds.Top,ActualWidth*scale,ActualHeight*scale));dc.Pop();
            if(_inboxWindow.IsVisible)dc.DrawImage(RenderWindow(_inboxWindow),new Rect(railOrigin.X-bounds.Left,railOrigin.Y-bounds.Top,_inboxWindow.ActualWidth*scale,_inboxWindow.ActualHeight*scale));
        }
        RenderTargetBitmap bitmap=new((int)Math.Ceiling(bounds.Width),(int)Math.Ceiling(bounds.Height),96,96,PixelFormats.Pbgra32);bitmap.Render(drawing);
        PngBitmapEncoder png=new();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(directory,name));png.Save(file);return bitmap;
    }
    private static void SaveInboxSheet(string directory,string name,List<BitmapSource> images)
    {
        DrawingVisual visual=new();using(var dc=visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White,null,new Rect(0,0,1040,1240));
            double factor=Math.Min(490.0/images.Max(image=>image.PixelWidth),275.0/images.Max(image=>image.PixelHeight));
            for(int i=0;i<images.Count;i++)
            {
                var image=images[i];
                double x=(i%2)*520+15,y=(i/2)*310+25;
                dc.DrawText(new FormattedText($"{i+1:00}",System.Globalization.CultureInfo.InvariantCulture,System.Windows.FlowDirection.LeftToRight,new Typeface("Segoe UI"),14,Brushes.SlateGray,1),new(x,y-20));
                dc.DrawImage(image,new Rect(x,y,image.PixelWidth*factor,image.PixelHeight*factor));
            }
        }
        RenderTargetBitmap bitmap=new(1040,1240,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        PngBitmapEncoder png=new();png.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create(Path.Combine(directory,name));png.Save(output);
    }
}
