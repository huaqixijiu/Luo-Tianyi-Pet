using System.Security.Cryptography;
using System.Text.Json;
using LuoTianyiPet.Animation;

namespace LuoTianyiPet.Animation.Tests;

public sealed class AnimationCatalogTests
{
    [Fact]
    public void LoadValidatesAtlasAndReturnsAsset()
    {
        using TemporaryCatalog temporary = TemporaryCatalog.Create();

        AnimationCatalog catalog = AnimationCatalog.Load(temporary.AssetsRoot, temporary.CatalogPath);

        Assert.Equal("idle", catalog.GetRequired("idle").Id);
        Assert.Equal(temporary.AtlasPath, catalog.GetAtlasPath(catalog.GetRequired("idle")));
    }

    [Fact]
    public void LoadRejectsChangedAtlas()
    {
        using TemporaryCatalog temporary = TemporaryCatalog.Create();
        File.AppendAllText(temporary.AtlasPath, "changed");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => AnimationCatalog.Load(temporary.AssetsRoot, temporary.CatalogPath));

        Assert.Contains("hash", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadRejectsAtlasPathOutsideAssetsDirectory()
    {
        using TemporaryCatalog temporary = TemporaryCatalog.Create();
        string json = File.ReadAllText(temporary.CatalogPath)
            .Replace("animations/idle.png", "../outside.png", StringComparison.Ordinal);
        File.WriteAllText(temporary.CatalogPath, json);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => AnimationCatalog.Load(temporary.AssetsRoot, temporary.CatalogPath, verifyHashes: false));

        Assert.Contains("escapes", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TemporaryCatalog : IDisposable
    {
        private TemporaryCatalog(string root, string assetsRoot, string catalogPath, string atlasPath)
        {
            Root = root;
            AssetsRoot = assetsRoot;
            CatalogPath = catalogPath;
            AtlasPath = atlasPath;
        }

        public string Root { get; }

        public string AssetsRoot { get; }

        public string CatalogPath { get; }

        public string AtlasPath { get; }

        public static TemporaryCatalog Create()
        {
            string root = Path.Combine(Path.GetTempPath(), $"LuoTianyiPet.Animation.Tests-{Guid.NewGuid():N}");
            string assetsRoot = Path.Combine(root, "assets");
            string atlasPath = Path.Combine(assetsRoot, "animations", "idle.png");
            string catalogPath = Path.Combine(assetsRoot, "manifests", "animations.json");
            Directory.CreateDirectory(Path.GetDirectoryName(atlasPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(catalogPath)!);
            File.WriteAllBytes(atlasPath, [1, 2, 3, 4]);
            string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(atlasPath)));
            var document = new
            {
                schemaVersion = 1,
                animations = new[]
                {
                    new
                    {
                        id = "idle",
                        sourcePath = "source.gif",
                        sourceSha256 = "source-hash",
                        atlasPath = "animations/idle.png",
                        atlasSha256 = hash,
                        frameWidth = 2,
                        frameHeight = 2,
                        columns = 1,
                        rows = 1,
                        frameDurationsMilliseconds = new[] { 100 },
                        loopCount = 0,
                        displayWidth = 100,
                        displayHeight = 100,
                        anchorX = 0.5,
                        anchorY = 1.0,
                        alphaBounds = new[] { 0, 0, 2, 2 },
                    },
                },
            };
            File.WriteAllText(catalogPath, JsonSerializer.Serialize(document));
            return new TemporaryCatalog(root, assetsRoot, catalogPath, atlasPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
