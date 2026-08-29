using LuoTianyiPet.Animation;
using System.Text.Json;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("Usage: LuoTianyiPet.AssetCompiler <assets-root> [catalog-path]");
    return 2;
}

string assetsRoot = Path.GetFullPath(args[0]);
string catalogPath = args.Length == 2
    ? Path.GetFullPath(args[1])
    : Path.Combine(assetsRoot, "manifests", "animations.json");

try
{
    AnimationCatalog catalog = AnimationCatalog.Load(assetsRoot, catalogPath);
    Console.WriteLine($"Validated {catalog.Assets.Count} runtime animations.");
    foreach (AnimationAssetManifest asset in catalog.Assets.OrderBy(asset => asset.Id, StringComparer.Ordinal))
    {
        Console.WriteLine($"{asset.Id}: {asset.FrameDurationsMilliseconds.Count} frames, {asset.DisplayWidth}x{asset.DisplayHeight}");
    }

    return 0;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
