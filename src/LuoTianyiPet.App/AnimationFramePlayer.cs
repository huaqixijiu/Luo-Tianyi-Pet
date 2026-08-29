using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LuoTianyiPet.Animation;

namespace LuoTianyiPet.App;

internal sealed class AnimationFramePlayer : IDisposable
{
    private readonly Image _target;
    private readonly AnimationCatalog _catalog;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();
    private readonly Dictionary<string, CachedAnimation> _cache = new(StringComparer.Ordinal);
    private CachedAnimation? _current;
    private Action? _completed;
    private int _currentFrameIndex = -1;
    private bool _completionRaised;

    public AnimationFramePlayer(Image target, AnimationCatalog catalog)
    {
        _target = target;
        _catalog = catalog;
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(15),
        };
        _timer.Tick += OnTick;
    }

    public string? CurrentAnimationId => _current?.Manifest.Id;

    public AnimationAssetManifest Play(string animationId, Action? completed = null)
    {
        CachedAnimation animation = GetOrLoad(animationId);
        _current = animation;
        _completed = completed;
        _completionRaised = false;
        _currentFrameIndex = 0;
        _target.Source = animation.Frames[0];
        _stopwatch.Restart();
        _timer.Start();
        return animation.Manifest;
    }

    public void Stop()
    {
        _timer.Stop();
        _stopwatch.Stop();
        _current = null;
        _completed = null;
        _completionRaised = false;
        _currentFrameIndex = -1;
        _target.Source = null;
    }

    public void Dispose()
    {
        Stop();
        _timer.Tick -= OnTick;
        _cache.Clear();
    }

    private CachedAnimation GetOrLoad(string animationId)
    {
        if (_cache.TryGetValue(animationId, out CachedAnimation? cached))
        {
            return cached;
        }

        AnimationAssetManifest manifest = _catalog.GetRequired(animationId);
        BitmapImage atlas = new();
        atlas.BeginInit();
        atlas.CacheOption = BitmapCacheOption.OnLoad;
        atlas.UriSource = new Uri(_catalog.GetAtlasPath(manifest), UriKind.Absolute);
        atlas.EndInit();
        atlas.Freeze();

        List<BitmapSource> frames = new(manifest.FrameDurationsMilliseconds.Count);
        for (int index = 0; index < manifest.FrameDurationsMilliseconds.Count; index++)
        {
            int x = index % manifest.Columns * manifest.FrameWidth;
            int y = index / manifest.Columns * manifest.FrameHeight;
            CroppedBitmap frame = new(
                atlas,
                new System.Windows.Int32Rect(x, y, manifest.FrameWidth, manifest.FrameHeight));
            frame.Freeze();
            frames.Add(frame);
        }

        CachedAnimation animation = new(
            manifest,
            frames,
            new AnimationFrameTimeline(manifest.FrameDurationsMilliseconds, manifest.LoopCount));
        _cache.Add(animationId, animation);
        return animation;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_current is null)
        {
            return;
        }

        PlaybackFrame playbackFrame = _current.Timeline.GetFrame(_stopwatch.Elapsed);
        if (playbackFrame.Index != _currentFrameIndex)
        {
            _currentFrameIndex = playbackFrame.Index;
            _target.Source = _current.Frames[playbackFrame.Index];
        }

        if (!playbackFrame.IsCompleted || _completionRaised)
        {
            return;
        }

        _completionRaised = true;
        _timer.Stop();
        _stopwatch.Stop();
        Action? completed = _completed;
        _completed = null;
        completed?.Invoke();
    }

    private sealed record CachedAnimation(
        AnimationAssetManifest Manifest,
        IReadOnlyList<BitmapSource> Frames,
        AnimationFrameTimeline Timeline);
}
