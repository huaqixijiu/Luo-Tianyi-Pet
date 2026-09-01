using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LuoTianyiPet.App;

internal sealed class BunTargetWindow : Window
{
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _bob = new();
    private readonly DispatcherTimer _bobTimer;
    private Point _dragPointer;
    private double _dragLeft;
    private double _dragTop;
    private DateTimeOffset _startedAt = DateTimeOffset.Now;

    public BunTargetWindow(string imagePath)
    {
        Width = 64;
        Height = 64;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        Topmost = true;

        BitmapImage bitmap = new();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        System.Windows.Controls.Image image = new()
        {
            Source = bitmap,
            Stretch = Stretch.Uniform,
            Cursor = System.Windows.Input.Cursors.Hand,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new TransformGroup
            {
                Children = new TransformCollection { _scale, _bob },
            },
        };
        image.MouseLeftButtonDown += OnMouseLeftButtonDown;
        image.MouseMove += OnMouseMove;
        image.MouseLeftButtonUp += OnMouseLeftButtonUp;
        Content = image;

        _bobTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(40),
        };
        _bobTimer.Tick += (_, _) =>
        {
            double phase = (DateTimeOffset.Now - _startedAt).TotalSeconds * Math.PI * 2 / 1.45;
            _bob.Y = Math.Sin(phase) * 3;
        };
        _bobTimer.Start();
    }

    public Point ScreenCenter => new(Left + Width / 2, Top + Height / 2);

    public bool IsBeingDragged { get; private set; }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        IsBeingDragged = true;
        _dragPointer = PointToScreen(e.GetPosition(this));
        _dragLeft = Left;
        _dragTop = Top;
        Mouse.Capture((IInputElement)sender);
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!IsBeingDragged || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point current = PointToScreen(e.GetPosition(this));
        Left = _dragLeft + current.X - _dragPointer.X;
        Top = _dragTop + current.Y - _dragPointer.Y;
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        IsBeingDragged = false;
        Mouse.Capture(null);
        e.Handled = true;
    }

    public async Task FlyIntoAsync(Point targetScreen, TimeSpan duration)
    {
        IsHitTestVisible = false;
        _bobTimer.Stop();
        Point start = ScreenCenter;
        DateTimeOffset started = DateTimeOffset.Now;
        while (DateTimeOffset.Now - started < duration)
        {
            double t = (DateTimeOffset.Now - started).TotalMilliseconds / duration.TotalMilliseconds;
            double eased = 1 - Math.Pow(1 - Math.Clamp(t, 0, 1), 3);
            double arc = Math.Sin(eased * Math.PI) * 22;
            Point centre = new(
                start.X + (targetScreen.X - start.X) * eased,
                start.Y + (targetScreen.Y - start.Y) * eased - arc);
            Left = centre.X - Width / 2;
            Top = centre.Y - Height / 2;
            double scale = 1 - eased * 0.82;
            _scale.ScaleX = scale;
            _scale.ScaleY = scale;
            Opacity = 1 - eased * 0.72;
            await Task.Delay(16);
        }

        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        _bobTimer.Stop();
        base.OnClosed(e);
    }
}
