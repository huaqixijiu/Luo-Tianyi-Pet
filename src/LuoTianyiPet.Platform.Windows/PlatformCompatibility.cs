namespace LuoTianyiPet.Platform.Windows;

internal static class PlatformCompatibility
{
    public static bool IsPathFullyQualified(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return Path.IsPathRooted(path) && !string.IsNullOrEmpty(Path.GetPathRoot(path));
    }

    public static string TrimEndingDirectorySeparator(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
