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
    private readonly Action<string, Exception>? _playbackFailed;
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
    private BitmapSource? _displayedFrame;

    public AnimationFramePlayer(
        Image target,
        AnimationCatalog catalog,
        Action<string, Exception>? playbackFailed = null)
    {
        _target = target;
        _catalog = catalog;
        _playbackFailed = playbackFailed;
    }

    public string? CurrentAnimationId => _current?.Manifest.Id;

    public int CurrentFrameIndex => _currentFrameIndex;

    public AnimationAssetManifest Play(
        string animationId,
        Action? completed = null,
        bool reverse = false,
        double playbackRate = 1.0)
    {
        AnimationAssetManifest manifest = _catalog.GetRequired(animationId);
        int initialFrameIndex = reverse
            ? manifest.FrameDurationsMilliseconds.Count - 1
            : 0;
        CachedAnimation animation = GetOrLoad(animationId, initialFrameIndex);
        int start = reverse ? animation.FrameCount - 1 : 0;
        int end = reverse ? 0 : animation.FrameCount - 1;
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
        CachedAnimation animation = GetOrLoad(animationId, startFrameIndex);
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
        CachedAnimation animation = GetOrLoad(animationId, frameIndex);
        if ((uint)frameIndex >= (uint)animation.FrameCount)
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
        ShowBestAvailableFrame(animation, frameIndex);
        if (!animation.IsFrameReady(frameIndex))
        {
            StartRendering();
        }
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
        _displayedFrame = null;
        _target.Source = null;
    }

    public void Dispose()
    {
        Stop();
        foreach (CachedAnimation animation in _cache.Values)
        {
            animation.Dispose();
        }
        _cache.Clear();
    }

    private CachedAnimation GetOrLoad(string animationId, int initialFrameIndex)
    {
        if (_cache.TryGetValue(animationId, out CachedAnimation? cached))
        {
            cached.LastAccess = ++_cacheAccessSequence;
            return cached;
        }

        AnimationAssetManifest manifest = _catalog.GetRequired(animationId);
        string assetPath = _catalog.GetAtlasPath(manifest);
        CachedAnimation animation;
        if (Path.GetExtension(assetPath).Equals(".webp", StringComparison.OrdinalIgnoreCase))
        {
            animation = new CachedAnimation(
                manifest,
                AnimatedWebpFrameDecoder.StartDecode(
                    assetPath,
                    manifest,
                    initialFrameIndex),
                EstimateDecodedBytes(manifest),
                ++_cacheAccessSequence);
        }
        else
        {
            animation = new CachedAnimation(
                manifest,
                LoadPngAtlas(assetPath, manifest),
                EstimateDecodedBytes(manifest),
                ++_cacheAccessSequence);
        }
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
                .OrderBy(animation => animation.LastAccess)
                .FirstOrDefault();
            if (oldest is null)
            {
                return;
            }

            _cache.Remove(oldest.Manifest.Id);
            oldest.Dispose();
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
        if (_current is null)
        {
            return;
        }

        if (_current.TakeDecodeFailure() is Exception decodeFailure)
        {
            FailPlayback(_current, decodeFailure);
            return;
        }

        if (_activeTimeline is null || _activeFrameIndices is null)
        {
            if (_currentFrameIndex >= 0)
            {
                ShowBestAvailableFrame(_current, _currentFrameIndex);
                if (_current.IsFrameReady(_currentFrameIndex))
                {
                    StopRendering();
                }
            }
            return;
        }

        PlaybackFrame playbackFrame = _activeTimeline.GetFrame(_stopwatch.Elapsed);
        int frameIndex = _activeFrameIndices[playbackFrame.Index];
        _currentFrameIndex = frameIndex;
        ShowBestAvailableFrame(_current, frameIndex);

        if (!playbackFrame.IsCompleted || _completionRaised ||
            !_current.IsFrameReady(frameIndex))
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

    private void ShowBestAvailableFrame(CachedAnimation animation, int frameIndex)
    {
        BitmapSource frame = animation.GetBestAvailableFrame(frameIndex);
        if (!ReferenceEquals(frame, _displayedFrame))
        {
            _displayedFrame = frame;
            _target.Source = frame;
        }
    }

    private void FailPlayback(CachedAnimation animation, Exception exception)
    {
        string animationId = animation.Manifest.Id;
        StopRendering();
        _stopwatch.Stop();
        _cache.Remove(animationId);
        animation.Dispose();
        _current = null;
        _activeTimeline = null;
        _activeFrameIndices = null;
        _completed = null;
        _completionRaised = false;
        _currentFrameIndex = -1;
        _displayedFrame = null;
        _target.Source = null;
        _playbackFailed?.Invoke(animationId, exception);
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
            animation.FrameCount);
        int[] durations = indices
            .Select(index => animation.Manifest.FrameDurationsMilliseconds[index])
            .ToArray();

        _current = animation;
        _activeFrameIndices = indices;
        _activeTimeline = new AnimationFrameTimeline(durations, loopCount, playbackRate);
        _completed = completed;
        _completionRaised = false;
        _currentFrameIndex = startFrameIndex;
        ShowBestAvailableFrame(animation, startFrameIndex);
        TrimDecodedCache();
        _stopwatch.Restart();
        StartRendering();
        return animation.Manifest;
    }

    private sealed class CachedAnimation : IDisposable
    {
        private readonly IReadOnlyList<BitmapSource>? _frames;
        private readonly AnimatedWebpFrameDecoder.ProgressiveBitmapFrames? _progressiveFrames;

        public CachedAnimation(
            AnimationAssetManifest manifest,
            IReadOnlyList<BitmapSource> frames,
            long estimatedDecodedBytes,
            long lastAccess)
        {
            Manifest = manifest;
            _frames = frames;
            EstimatedDecodedBytes = estimatedDecodedBytes;
            LastAccess = lastAccess;
        }

        public CachedAnimation(
            AnimationAssetManifest manifest,
            AnimatedWebpFrameDecoder.ProgressiveBitmapFrames progressiveFrames,
            long estimatedDecodedBytes,
            long lastAccess)
        {
            Manifest = manifest;
            _progressiveFrames = progressiveFrames;
            EstimatedDecodedBytes = estimatedDecodedBytes;
            LastAccess = lastAccess;
        }

        public AnimationAssetManifest Manifest { get; }

        public int FrameCount => _frames?.Count ?? _progressiveFrames!.Count;

        public long EstimatedDecodedBytes { get; }

        public long LastAccess { get; set; }

        public bool IsFrameReady(int frameIndex) =>
            _frames is not null || _progressiveFrames!.IsFrameReady(frameIndex);

        public BitmapSource GetBestAvailableFrame(int frameIndex) =>
            _frames is not null
                ? _frames[frameIndex]
                : _progressiveFrames!.GetBestAvailableFrame(frameIndex);

        public Exception? TakeDecodeFailure() => _progressiveFrames?.TakeFailure();

        public void Dispose() => _progressiveFrames?.Dispose();
    }
}
