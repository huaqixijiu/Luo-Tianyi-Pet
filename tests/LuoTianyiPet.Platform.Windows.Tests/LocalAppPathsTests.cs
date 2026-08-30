using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.Platform.Windows.Tests;

public sealed class LocalAppPathsTests
{
    [Fact]
    public void CreatePortable_StoresAllMutableDataUnderApplicationDirectory()
    {
        string applicationDirectory = Path.Combine(
            Path.GetTempPath(),
            "LuoTianyiPet Portable Test");

        LocalAppPaths paths = LocalAppPaths.CreatePortable(applicationDirectory);

        string expectedRoot = Path.Combine(
            Path.GetFullPath(applicationDirectory),
            LocalAppPaths.PortableDataDirectoryName);
        Assert.Equal(expectedRoot, paths.RootDirectory);
        Assert.Equal(Path.Combine(expectedRoot, "settings.json"), paths.SettingsFile);
        Assert.Equal(Path.Combine(expectedRoot, "logs"), paths.LogsDirectory);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreatePortable_RejectsMissingApplicationDirectory(string value)
    {
        Assert.Throws<ArgumentException>(() => LocalAppPaths.CreatePortable(value));
    }
}
