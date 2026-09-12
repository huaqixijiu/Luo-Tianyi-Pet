using System.IO;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuoTianyiPet.Animation;
using SkiaSharp;

namespace LuoTianyiPet.App;

internal static class AnimatedWebpFrameDecoder
{
    private static readonly SemaphoreSlim DecodeGate = new(1, 1);

    public static ProgressiveBitmapFrames StartDecode(
        string path,
        AnimationAssetManifest manifest,
        int initialFrameIndex)
    {
        using SKCodec codec = CreateAndValidateCodec(path, manifest);
        int[] logicalToEncodedFrame = BuildLogicalFrameMap(codec, manifest);
        int safeInitialFrame = Math.Max(
            0,
            Math.Min(initialFrameIndex, logicalToEncodedFrame.Length - 1));
        BitmapSource initialFrame = DecodeSingleFrame(
            codec,
            manifest,
            logicalToEncodedFrame[safeInitialFrame]);

        ProgressiveBitmapFrames frames = new(
            manifest.FrameDurationsMilliseconds.Count,
            logicalToEncodedFrame,
            safeInitialFrame,
            initialFrame);
        frames.Start(path, manifest);
        return frames;
    }

    private static void DecodeAllFrames(
        string path,
        AnimationAssetManifest manifest,
        ProgressiveBitmapFrames destination,
        CancellationToken cancellationToken)
    {
        bool enteredGate = false;
        Thread currentThread = Thread.CurrentThread;
        ThreadPriority originalPriority = currentThread.Priority;
        try
        {
            currentThread.Priority = ThreadPriority.BelowNormal;
            DecodeGate.Wait(cancellationToken);
            enteredGate = true;

            using SKCodec codec = CreateAndValidateCodec(path, manifest);
            int[] logicalToEncodedFrame = BuildLogicalFrameMap(codec, manifest);
            SKImageInfo imageInfo = CreateImageInfo(manifest);
            SKCodecFrameInfo[] encodedFrameInfo = codec.FrameInfo;
            using SKBitmap bitmap = new(imageInfo);
            for (int encodedIndex = 0; encodedIndex < codec.FrameCount; encodedIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int priorFrame = encodedIndex > 0 &&
                    encodedFrameInfo[encodedIndex - 1].DisposalMethod !=
                        SKCodecAnimationDisposalMethod.RestorePrevious
                    ? encodedIndex - 1
                    : -1;
                SKCodecResult result = codec.GetPixels(
                    imageInfo,
                    bitmap.GetPixels(),
                    new SKCodecOptions(encodedIndex, priorFrame));
                ValidateDecodeResult(manifest, encodedIndex, result);

                BitmapSource frame = CreateBitmapSource(bitmap, imageInfo);
                for (int logicalIndex = 0;
                     logicalIndex < logicalToEncodedFrame.Length;
                     logicalIndex++)
                {
                    if (logicalToEncodedFrame[logicalIndex] == encodedIndex)
                    {
                        destination.Publish(logicalIndex, frame);
                    }
                }
            }

            destination.Complete();
        }
        catch (OperationCanceledException)
        {
            destination.Cancel();
        }
        catch (Exception exception)
        {
            destination.Fail(exception);
        }
        finally
        {
            if (enteredGate)
            {
                DecodeGate.Release();
            }

            currentThread.Priority = originalPriority;
        }
    }

    private static SKCodec CreateAndValidateCodec(
        string path,
        AnimationAssetManifest manifest)
    {
        SKCodec codec = SKCodec.Create(path)
            ?? throw new InvalidDataException($"Unable to decode animation '{manifest.Id}'.");
        int frameCount = codec.FrameCount > 0 ? codec.FrameCount : 1;
        int declaredFrameCount = manifest.FrameDurationsMilliseconds.Count;
        if (frameCount > declaredFrameCount)
        {
            codec.Dispose();
            throw new InvalidDataException(
                $"Animation '{manifest.Id}' declares {declaredFrameCount} " +
                $"frames but its WebP contains {frameCount}.");
        }

        if (codec.Info.Width != manifest.FrameWidth || codec.Info.Height != manifest.FrameHeight)
        {
            codec.Dispose();
            throw new InvalidDataException(
                $"Animation '{manifest.Id}' declares {manifest.FrameWidth}x{manifest.FrameHeight} " +
                $"frames but its WebP contains {codec.Info.Width}x{codec.Info.Height} frames.");
        }

        return codec;
    }

    private static int[] BuildLogicalFrameMap(
        SKCodec codec,
        AnimationAssetManifest manifest)
    {
        int encodedFrameCount = codec.FrameCount > 0 ? codec.FrameCount : 1;
        int declaredFrameCount = manifest.FrameDurationsMilliseconds.Count;
        int[] logicalToEncodedFrame = new int[declaredFrameCount];
        if (encodedFrameCount == declaredFrameCount)
        {
            for (int index = 0; index < declaredFrameCount; index++)
            {
                logicalToEncodedFrame[index] = index;
            }

            return logicalToEncodedFrame;
        }

        SKCodecFrameInfo[] encodedFrameInfo = codec.FrameInfo;
        int logicalIndex = 0;
        for (int encodedIndex = 0;
             encodedIndex < encodedFrameCount && logicalIndex < declaredFrameCount;
             encodedIndex++)
        {
            int encodedDuration = encodedFrameInfo[encodedIndex].Duration;
            while (encodedDuration > 0 && logicalIndex < declaredFrameCount)
            {
                int declaredDuration = manifest.FrameDurationsMilliseconds[logicalIndex];
                if (encodedDuration < declaredDuration)
                {
                    break;
                }

                logicalToEncodedFrame[logicalIndex] = encodedIndex;
                logicalIndex++;
                encodedDuration -= declaredDuration;
            }
        }

        if (logicalIndex != declaredFrameCount)
        {
            throw new InvalidDataException(
                $"Animation '{manifest.Id}' could not expand {encodedFrameCount} encoded WebP " +
                $"frames to its {declaredFrameCount}-frame timeline.");
        }

        return logicalToEncodedFrame;
    }

    private static BitmapSource DecodeSingleFrame(
        SKCodec codec,
        AnimationAssetManifest manifest,
        int encodedFrameIndex)
    {
        SKImageInfo imageInfo = CreateImageInfo(manifest);
        using SKBitmap bitmap = new(imageInfo);
        SKCodecResult result = codec.GetPixels(
            imageInfo,
            bitmap.GetPixels(),
            new SKCodecOptions(encodedFrameIndex));
        ValidateDecodeResult(manifest, encodedFrameIndex, result);
        return CreateBitmapSource(bitmap, imageInfo);
    }

    private static SKImageInfo CreateImageInfo(AnimationAssetManifest manifest) => new(
        manifest.FrameWidth,
        manifest.FrameHeight,
        SKColorType.Bgra8888,
        SKAlphaType.Premul);

    private static void ValidateDecodeResult(
        AnimationAssetManifest manifest,
        int encodedFrameIndex,
        SKCodecResult result)
    {
        if (result is not SKCodecResult.Success and not SKCodecResult.IncompleteInput)
        {
            throw new InvalidDataException(
                $"Animation '{manifest.Id}' frame {encodedFrameIndex} failed to decode: {result}.");
        }
    }

    private static BitmapSource CreateBitmapSource(SKBitmap bitmap, SKImageInfo imageInfo)
    {
        BitmapSource frame = BitmapSource.Create(
            imageInfo.Width,
            imageInfo.Height,
            96,
            96,
            PixelFormats.Pbgra32,
            null,
            bitmap.GetPixels(),
            bitmap.ByteCount,
            bitmap.RowBytes);
        frame.Freeze();
        return frame;
    }

    internal sealed class ProgressiveBitmapFrames : IDisposable
    {
        private readonly BitmapSource?[] _frames;
        private readonly CancellationTokenSource _cancellation = new();
        private Exception? _failure;
        private int _isComplete;
        private int _isDisposed;

        public ProgressiveBitmapFrames(
            int frameCount,
            int[] logicalToEncodedFrame,
            int initialFrameIndex,
            BitmapSource initialFrame)
        {
            _frames = new BitmapSource?[frameCount];
            int encodedInitialFrame = logicalToEncodedFrame[initialFrameIndex];
            for (int logicalIndex = 0; logicalIndex < logicalToEncodedFrame.Length; logicalIndex++)
            {
                if (logicalToEncodedFrame[logicalIndex] == encodedInitialFrame)
                {
                    _frames[logicalIndex] = initialFrame;
                }
            }
        }

        public int Count => _frames.Length;

        public bool IsComplete => Volatile.Read(ref _isComplete) != 0;

        public bool IsFrameReady(int frameIndex) =>
            Volatile.Read(ref _frames[frameIndex]) is not null;

        public BitmapSource GetBestAvailableFrame(int frameIndex)
        {
            BitmapSource? exact = Volatile.Read(ref _frames[frameIndex]);
            if (exact is not null)
            {
                return exact;
            }

            for (int distance = 1; distance < _frames.Length; distance++)
            {
                int earlier = frameIndex - distance;
                if (earlier >= 0 && Volatile.Read(ref _frames[earlier]) is BitmapSource prior)
                {
                    return prior;
                }

                int later = frameIndex + distance;
                if (later < _frames.Length && Volatile.Read(ref _frames[later]) is BitmapSource next)
                {
                    return next;
                }
            }

            throw new InvalidDataException("The animation has no decoded frame available.");
        }

        public Exception? TakeFailure() => Interlocked.Exchange(ref _failure, null);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
            {
                _cancellation.Cancel();
            }
        }

        internal void Start(string path, AnimationAssetManifest manifest)
        {
            CancellationToken cancellationToken = _cancellation.Token;
            _ = Task.Factory.StartNew(
                () => DecodeAllFrames(path, manifest, this, cancellationToken),
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        internal void Publish(int frameIndex, BitmapSource frame)
        {
            if (Volatile.Read(ref _isDisposed) == 0)
            {
                Volatile.Write(ref _frames[frameIndex], frame);
            }
        }

        internal void Complete() => Volatile.Write(ref _isComplete, 1);

        internal void Cancel() => Volatile.Write(ref _isComplete, 1);

        internal void Fail(Exception exception)
        {
            Interlocked.CompareExchange(ref _failure, exception, null);
            Volatile.Write(ref _isComplete, 1);
        }
    }
}
