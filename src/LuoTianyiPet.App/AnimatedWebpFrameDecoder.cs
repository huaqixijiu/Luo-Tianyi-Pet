using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuoTianyiPet.Animation;
using SkiaSharp;

namespace LuoTianyiPet.App;

internal static class AnimatedWebpFrameDecoder
{
    public static IReadOnlyList<BitmapSource> Decode(
        string path,
        AnimationAssetManifest manifest)
    {
        using SKCodec codec = SKCodec.Create(path)
            ?? throw new InvalidDataException($"Unable to decode animation '{manifest.Id}'.");
        int frameCount = codec.FrameCount > 0 ? codec.FrameCount : 1;
        int declaredFrameCount = manifest.FrameDurationsMilliseconds.Count;
        if (frameCount > declaredFrameCount)
        {
            throw new InvalidDataException(
                $"Animation '{manifest.Id}' declares {declaredFrameCount} " +
                $"frames but its WebP contains {frameCount}.");
        }

        if (codec.Info.Width != manifest.FrameWidth || codec.Info.Height != manifest.FrameHeight)
        {
            throw new InvalidDataException(
                $"Animation '{manifest.Id}' declares {manifest.FrameWidth}x{manifest.FrameHeight} " +
                $"frames but its WebP contains {codec.Info.Width}x{codec.Info.Height} frames.");
        }

        SKImageInfo imageInfo = new(
            manifest.FrameWidth,
            manifest.FrameHeight,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);
        SKCodecFrameInfo[] encodedFrameInfo = codec.FrameInfo;
        List<BitmapSource> frames = new(declaredFrameCount);
        for (int index = 0; index < frameCount; index++)
        {
            using SKBitmap bitmap = new(imageInfo);
            SKCodecResult result = codec.GetPixels(
                imageInfo,
                bitmap.GetPixels(),
                new SKCodecOptions(index));
            if (result is not SKCodecResult.Success and not SKCodecResult.IncompleteInput)
            {
                throw new InvalidDataException(
                    $"Animation '{manifest.Id}' frame {index} failed to decode: {result}.");
            }

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
            if (frameCount == declaredFrameCount)
            {
                frames.Add(frame);
                continue;
            }

            int encodedDuration = encodedFrameInfo[index].Duration;
            while (encodedDuration > 0 && frames.Count < declaredFrameCount)
            {
                int declaredDuration = manifest.FrameDurationsMilliseconds[frames.Count];
                if (encodedDuration < declaredDuration)
                {
                    break;
                }

                frames.Add(frame);
                encodedDuration -= declaredDuration;
            }
        }

        if (frames.Count != declaredFrameCount)
        {
            throw new InvalidDataException(
                $"Animation '{manifest.Id}' could not expand {frameCount} encoded WebP " +
                $"frames to its {declaredFrameCount}-frame timeline.");
        }

        return frames;
    }
}
