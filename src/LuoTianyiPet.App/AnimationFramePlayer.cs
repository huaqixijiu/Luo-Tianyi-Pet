using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuoTianyiPet.Animation;

namespace LuoTianyiPet.App;

internal sealed class AnimationFramePlayer : IDisposable
{
    private readonly Image _target;
    private readonly AnimationCatalog _catalog;
    private readonly Stopwatch _stopwatch = new();
    private readonly Dictionary<string, CachedAnimation> _cache = new(StringComparer.Ordinal);
    private const long DecodedCacheBudgetBytes = 192L * 1024 * 1024;
    private long _cacheAccessSequence;
    private CachedAnimation? _current;
    private AnimationFrameTimeline? _activeTimeline;
    private IReadOnlyList<int>? _activeFrameIndices;
    private Action? _completed;
    private int _currentFrameIndex = -1;
    private bool _completionRaised;
    private bool _renderingSubscribed;

    public AnimationFramePlayer(Image target, AnimationCatalog catalog)
    {
        _target = target;
        _catalog = catalog;
    }

    public string? CurrentAnimationId => _current?.Manifest.Id;

    public int CurrentFrameIndex => _currentFrameIndex;

    public AnimationAssetManifest Play(
        string animationId,
        Action? completed = null,
        bool reverse = false,
        double playbackRate = 1.0)
    {
        CachedAnimation animation = GetOrLoad(animationId);
        int start = reverse ? animation.Frames.Count - 1 : 0;
        int end = reverse ? 0 : animation.Frames.Count - 1;
        return StartPlayback(
            animation,
            start,
            end,
            animation.Manifest.LoopCount,
            completed,
            playbackRate);
    }

    public AnimationAssetManifest PlayRange(
        string animationId,
        int startFrameIndex,
        int endFrameIndex,
        Action? completed = null,
        double playbackRate = 1.0)
    {
        CachedAnimation animation = GetOrLoad(animationId);
        return StartPlayback(
            animation,
            startFrameIndex,
            endFrameIndex,
            loopCount: 1,
            completed,
            playbackRate);
    }

    public AnimationAssetManifest ShowFrame(string animationId, int frameIndex)
    {
        CachedAnimation animation = GetOrLoad(animationId);
        if ((uint)frameIndex >= (uint)animation.Frames.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex));
        }

        StopRendering();
        _stopwatch.Stop();
        _current = animation;
        _activeTimeline = null;
        _activeFrameIndices = null;
        _completed = null;
        _completionRaised = false;
        _currentFrameIndex = frameIndex;
        _target.Source = animation.Frames[frameIndex];
        TrimDecodedCache();
        return animation.Manifest;
    }

    public void Stop()
    {
        StopRendering();
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
        _cache.Clear();
    }

    private CachedAnimation GetOrLoad(string animationId)
    {
        if (_cache.TryGetValue(animationId, out CachedAnimation? cached))
        {
            cached.LastAccess = ++_cacheAccessSequence;
            return cached;
        }

        AnimationAssetManifest manifest = _catalog.GetRequired(animationId);
        string assetPath = _catalog.GetAtlasPath(manifest);
        IReadOnlyList<BitmapSource> frames = Path.GetExtension(assetPath)
            .Equals(".webp", StringComparison.OrdinalIgnoreCase)
            ? AnimatedWebpFrameDecoder.Decode(assetPath, manifest)
            : LoadPngAtlas(assetPath, manifest);

        CachedAnimation animation = new(
            manifest,
            frames,
            EstimateDecodedBytes(manifest),
            ++_cacheAccessSequence);
        _cache.Add(animationId, animation);
        return animation;
    }

    private static IReadOnlyList<BitmapSource> LoadPngAtlas(
        string assetPath,
        AnimationAssetManifest manifest)
    {
        BitmapImage atlas = new();
        atlas.BeginInit();
        atlas.CacheOption = BitmapCacheOption.OnLoad;
        atlas.UriSource = new Uri(assetPath, UriKind.Absolute);
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
        return frames;
    }

    private static long EstimateDecodedBytes(AnimationAssetManifest manifest) =>
        checked((long)manifest.FrameWidth * manifest.FrameHeight * 4 *
            manifest.FrameDurationsMilliseconds.Count);

    private void TrimDecodedCache()
    {
        while (_cache.Values.Sum(animation => animation.EstimatedDecodedBytes) >
               DecodedCacheBudgetBytes)
        {
            CachedAnimation? oldest = _cache.Values
                .Where(animation => !ReferenceEquals(animation, _current))
                .MinBy(animation => animation.LastAccess);
            if (oldest is null)
            {
                return;
            }

            _cache.Remove(oldest.Manifest.Id);
        }
    }

    private void StartRendering()
    {
        if (_renderingSubscribed)
        {
            return;
        }

        CompositionTarget.Rendering += OnRendering;
        _renderingSubscribed = true;
    }

    private void StopRendering()
    {
        if (!_renderingSubscribed)
        {
            return;
        }

        CompositionTarget.Rendering -= OnRendering;
        _renderingSubscribed = false;
    }

    private void OnRendering(object? sender, EventArgs e)
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
        StopRendering();
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
        Action? completed,
        double playbackRate = 1.0)
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
        _activeTimeline = new AnimationFrameTimeline(durations, loopCount, playbackRate);
        _completed = completed;
        _completionRaised = false;
        _currentFrameIndex = startFrameIndex;
        _target.Source = animation.Frames[startFrameIndex];
        TrimDecodedCache();
        _stopwatch.Restart();
        StartRendering();
        return animation.Manifest;
    }

    private sealed class CachedAnimation(
        AnimationAssetManifest manifest,
        IReadOnlyList<BitmapSource> frames,
        long estimatedDecodedBytes,
        long lastAccess)
    {
        public AnimationAssetManifest Manifest { get; } = manifest;

        public IReadOnlyList<BitmapSource> Frames { get; } = frames;

        public long EstimatedDecodedBytes { get; } = estimatedDecodedBytes;

        public long LastAccess { get; set; } = lastAccess;
    }
}
