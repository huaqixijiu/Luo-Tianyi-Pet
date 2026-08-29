namespace LuoTianyiPet.Platform.Windows;

public sealed class LocalAppPaths
{
    public LocalAppPaths(string? rootDirectory = null)
    {
        RootDirectory = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LuoTianyiPet");
    }

    public string RootDirectory { get; }

    public string SettingsFile => Path.Combine(RootDirectory, "settings.json");

    public string LogsDirectory => Path.Combine(RootDirectory, "logs");
}
