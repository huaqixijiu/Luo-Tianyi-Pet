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
    private AnimationFrameTimeline? _activeTimeline;
    private IReadOnlyList<int>? _activeFrameIndices;
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

    public int CurrentFrameIndex => _currentFrameIndex;

    public AnimationAssetManifest Play(
        string animationId,
        Action? completed = null,
        bool reverse = false)
    {
        CachedAnimation animation = GetOrLoad(animationId);
        int start = reverse ? animation.Frames.Count - 1 : 0;
        int end = reverse ? 0 : animation.Frames.Count - 1;
        return StartPlayback(animation, start, end, animation.Manifest.LoopCount, completed);
    }

    public AnimationAssetManifest PlayRange(
        string animationId,
        int startFrameIndex,
        int endFrameIndex,
        Action? completed = null)
    {
        CachedAnimation animation = GetOrLoad(animationId);
        return StartPlayback(animation, startFrameIndex, endFrameIndex, loopCount: 1, completed);
    }

    public AnimationAssetManifest ShowFrame(string animationId, int frameIndex)
    {
        CachedAnimation animation = GetOrLoad(animationId);
        if ((uint)frameIndex >= (uint)animation.Frames.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex));
        }

        _timer.Stop();
        _stopwatch.Stop();
        _current = animation;
        _activeTimeline = null;
        _activeFrameIndices = null;
        _completed = null;
        _completionRaised = false;
        _currentFrameIndex = frameIndex;
        _target.Source = animation.Frames[frameIndex];
        return animation.Manifest;
    }

    public void Stop()
    {
        _timer.Stop();
        _stopwatch.Stop();
        _current = null;
        _activeTimeline = null;
        _activeFrameIndices = null;
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

        CachedAnimation animation = new(manifest, frames);
        _cache.Add(animationId, animation);
        return animation;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_current is null || _activeTimeline is null || _activeFrameIndices is null)
        {
            return;
        }

        PlaybackFrame playbackFrame = _activeTimeline.GetFrame(_stopwatch.Elapsed);
        int frameIndex = _activeFrameIndices[playbackFrame.Index];
        if (frameIndex != _currentFrameIndex)
        {
            _currentFrameIndex = frameIndex;
            _target.Source = _current.Frames[frameIndex];
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

    private AnimationAssetManifest StartPlayback(
        CachedAnimation animation,
        int startFrameIndex,
        int endFrameIndex,
        int loopCount,
        Action? completed)
    {
        IReadOnlyList<int> indices = FrameIndexSequence.Create(
            startFrameIndex,
            endFrameIndex,
            animation.Frames.Count);
        int[] durations = indices
            .Select(index => animation.Manifest.FrameDurationsMilliseconds[index])
            .ToArray();

        _current = animation;
        _activeFrameIndices = indices;
        _activeTimeline = new AnimationFrameTimeline(durations, loopCount);
        _completed = completed;
        _completionRaised = false;
        _currentFrameIndex = startFrameIndex;
        _target.Source = animation.Frames[startFrameIndex];
        _stopwatch.Restart();
        _timer.Start();
        return animation.Manifest;
    }

    private sealed record CachedAnimation(
        AnimationAssetManifest Manifest,
        IReadOnlyList<BitmapSource> Frames);
}
