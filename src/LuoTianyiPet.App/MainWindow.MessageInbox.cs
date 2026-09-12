using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private readonly MessageInbox _messageInbox = new();
    private MessageInboxWindow? _inboxWindow;
    private MessageRailSide? _inboxSide;
    private bool _inboxSafe;
    private void RefreshMessageInbox()
    {
        if (_isClosing) return;
        if (_inboxWindow is null)
        {
            _inboxWindow = new(_messageInbox) { Owner = this };
            _inboxWindow.PositionRequested += PositionMessageInbox;
            _inboxWindow.Changed += () => _inboxWindow.Refresh();
        }
        _inboxWindow.Refresh();
    }

    private bool IsInboxDisplaySafe(ForegroundApplicationSnapshot foreground) =>
        !_isClosing && !_hiddenByUser && _settings.Notifications.EnableMessageReminders &&
        foreground.Succeeded && !foreground.IsFullscreen && !_systemSessionUnavailable &&
        _edgeDockSide == EdgeDockSide.None && _stateMachine.VisualState.ContinuousState is not
            (PetContinuousState.Sleeping or PetContinuousState.HiddenForSafety);

    private void PruneMessageInbox()
    {
        _messageInbox.Prune(message => message.Provider == MessageProvider.WeChat &&
            (message.WeChatSessionKey is string key
                ? _weChatSessionSource?.IsConversationUnread(key) == false
                : _weChatSessionSource?.HasUnreadConversations == false));
        RefreshMessageInbox();
    }

    private void PositionMessageInbox()
    {
        if (_inboxWindow is null) return;
        int count = new[] {MessageProvider.WeChat,MessageProvider.Qq}.Count(p => _messageInbox.Total(p)>0);
        if (!_inboxSafe || count == 0 || _isClosing || _hiddenByUser || _edgeDockSide != EdgeDockSide.None)
        { _inboxWindow.Suspend(); return; }
        UpdateLayout();
        var work = _windowWorkAreaProvider.GetForWindow(new WindowInteropHelper(this).Handle);
        var local = GetPetImageAlphaBoundsInWindow();
        Point top = PointToScreen(new(local.Left,local.Top)), bottom = PointToScreen(new(local.Right,local.Bottom));
        var silhouette = new DesktopRectangle(top.X,top.Y,bottom.X-top.X,bottom.Y-top.Y);
        double ratio = _settings.Appearance.FullBodyStyle == AppearanceOptionIds.FullBodyClassicCatEars ? .23 :
            _settings.Appearance.FullBodyStyle == AppearanceOptionIds.FullBodyCrystalDress ? .25 : .05;
        double earY = top.Y + silhouette.Height * ratio;
        double scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1;
        var contour = EarContour(local, ratio, 2);
        var a = PointToScreen(new(contour.Left,contour.Top)); var b = PointToScreen(new(contour.Right,contour.Bottom));
        var head = new DesktopRectangle(a.X,a.Y,b.X-a.X,b.Y-a.Y);
        _inboxSide = MessageRailPlacement.SideFor(silhouette, work, _inboxSide,
            _isWindowDragging || _inboxWindow.Interaction.OpenProvider is not null);
        int rows = _inboxWindow.Interaction.OpenProvider is MessageProvider provider ? _messageInbox.Rows(provider).Count : 0;
        var layout = MessageRailPlacement.Place(head,earY,work,_inboxSide.Value,count,rows,_inboxWindow.ActiveIconIndex(),scale);
        _inboxWindow.Topmost = Topmost;
        _inboxWindow.SetLayout(layout,scale);
    }

    private DesktopRectangle EarContour(DesktopRectangle fallback, double ratio, int icons)
    {
        if (PetImage.Source is not BitmapSource source || PetImage.ActualHeight <= 0 || source.PixelWidth < 1) return fallback;
        try
        {
            BitmapSource bitmap = source.Format == PixelFormats.Bgra32 || source.Format == PixelFormats.Pbgra32
                ? source : new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);
            // Read only our own artwork's alpha strip beside the calibrated ear height.
            Point localEar = PetImage.TransformToAncestor(this).Inverse!.Transform(new(fallback.Left,fallback.Top+fallback.Height*ratio));
            int y = Numeric.Clamp((int)((localEar.Y-4)/PetImage.ActualHeight*bitmap.PixelHeight),0,bitmap.PixelHeight-1);
            int height = Math.Min(bitmap.PixelHeight-y,Math.Max(1,(int)((icons*30-6)/PetImage.ActualHeight*bitmap.PixelHeight)));
            int stride = bitmap.PixelWidth*4; byte[] pixels = new byte[stride*height];
            bitmap.CopyPixels(new Int32Rect(0,y,bitmap.PixelWidth,height),pixels,stride,0);
            int left = bitmap.PixelWidth, right = 0;
            for (int row=0;row<height;row++) for(int x=0;x<bitmap.PixelWidth;x++)
                if(pixels[row*stride+x*4+3]>=24){left=Math.Min(left,x);right=Math.Max(right,x+1);}
            if(right<=left) return fallback;
            Rect band = PetImage.TransformToAncestor(this).TransformBounds(new Rect(
                left*PetImage.ActualWidth/bitmap.PixelWidth,y*PetImage.ActualHeight/bitmap.PixelHeight,
                (right-left)*PetImage.ActualWidth/bitmap.PixelWidth,height*PetImage.ActualHeight/bitmap.PixelHeight));
            return new(band.Left,band.Top,band.Width,band.Height);
        }
        catch (Exception) { return fallback; }
    }
}
