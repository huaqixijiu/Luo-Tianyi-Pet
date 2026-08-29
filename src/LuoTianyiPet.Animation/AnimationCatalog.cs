using System.Security.Cryptography;
using System.Text.Json;

namespace LuoTianyiPet.Animation;

public sealed class AnimationCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IReadOnlyDictionary<string, AnimationAssetManifest> _assets;
    private readonly IReadOnlyList<AnimationAssetManifest> _assetList;

    private AnimationCatalog(string assetsRoot, IReadOnlyDictionary<string, AnimationAssetManifest> assets)
    {
        AssetsRoot = assetsRoot;
        _assets = assets;
        _assetList = assets.Values.ToArray();
    }

    public string AssetsRoot { get; }

    public IReadOnlyList<AnimationAssetManifest> Assets => _assetList;

    public AnimationAssetManifest GetRequired(string id) =>
        _assets.TryGetValue(id, out AnimationAssetManifest? asset)
            ? asset
            : throw new KeyNotFoundException($"Animation '{id}' is not in the runtime catalog.");

    public string GetAtlasPath(AnimationAssetManifest asset) =>
        Path.GetFullPath(Path.Combine(AssetsRoot, asset.AtlasPath.Replace('/', Path.DirectorySeparatorChar)));

    public static AnimationCatalog Load(string assetsRoot, string catalogPath, bool verifyHashes = true)
    {
        string normalizedAssetsRoot = Path.GetFullPath(assetsRoot);
        string assetsRootPrefix = Path.TrimEndingDirectorySeparator(normalizedAssetsRoot) + Path.DirectorySeparatorChar;
        string json = File.ReadAllText(catalogPath);
        AnimationCatalogDocument document = JsonSerializer.Deserialize<AnimationCatalogDocument>(json, SerializerOptions)
            ?? throw new InvalidDataException("Animation catalog is empty.");

        if (document.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Unsupported animation catalog schema {document.SchemaVersion}.");
        }

        Dictionary<string, AnimationAssetManifest> assets = new(StringComparer.Ordinal);
        List<string> errors = [];
        foreach (AnimationAssetManifest asset in document.Animations)
        {
            errors.AddRange(asset.Validate());
            if (!assets.TryAdd(asset.Id, asset))
            {
                errors.Add($"Animation id '{asset.Id}' is duplicated.");
                continue;
            }

            string atlasPath = Path.GetFullPath(Path.Combine(
                normalizedAssetsRoot,
                asset.AtlasPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!atlasPath.StartsWith(assetsRootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Animation '{asset.Id}' atlas path escapes the assets directory.");
            }
            else if (!File.Exists(atlasPath))
            {
                errors.Add($"Animation '{asset.Id}' atlas is missing.");
            }
            else if (verifyHashes && !HashMatches(atlasPath, asset.AtlasSha256))
            {
                errors.Add($"Animation '{asset.Id}' atlas hash does not match the catalog.");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        }

        return new AnimationCatalog(normalizedAssetsRoot, assets);
    }

    private static bool HashMatches(string path, string expectedHash)
    {
        using FileStream stream = File.OpenRead(path);
        string actualHash = Convert.ToHexStringLower(SHA256.HashData(stream));
        return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record AnimationCatalogDocument
    {
        public required int SchemaVersion { get; init; }

        public required IReadOnlyList<AnimationAssetManifest> Animations { get; init; }
    }
}
