namespace LuoTianyiPet.Platform.Windows;

public sealed class LocalAppPaths
{
    public const string PortableDataDirectoryName = "UserData";

    public LocalAppPaths(string? rootDirectory = null)
    {
        RootDirectory = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LuoTianyiPet");
    }

    public string RootDirectory { get; }

    public string SettingsFile => Path.Combine(RootDirectory, "settings.json");

    public string LogsDirectory => Path.Combine(RootDirectory, "logs");

    public static LocalAppPaths CreatePortable(string applicationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
        string rootDirectory = Path.Combine(
            Path.GetFullPath(applicationDirectory),
            PortableDataDirectoryName);
        return new LocalAppPaths(rootDirectory);
    }
}
