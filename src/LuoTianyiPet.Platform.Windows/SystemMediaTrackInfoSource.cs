using System.Diagnostics;
using LuoTianyiPet.Core;
using Windows.Media.Control;

namespace LuoTianyiPet.Platform.Windows;

public sealed class SystemMediaTrackInfoSource : IMediaTrackInfoSource
{
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;

    public async ValueTask<MediaTrackSnapshot> ReadAsync(string targetProcessName)
    {
        if (string.IsNullOrWhiteSpace(targetProcessName))
        {
            return MediaTrackSnapshot.NoSession;
        }

        try
        {
            _sessionManager ??=
                await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            GlobalSystemMediaTransportControlsSession? session = _sessionManager
                .GetSessions()
                .FirstOrDefault(candidate => MatchesSourceApplication(
                    candidate.SourceAppUserModelId,
                    targetProcessName));
            if (session is null)
            {
                return ReadFromWindowTitle(targetProcessName);
            }

            GlobalSystemMediaTransportControlsSessionMediaProperties properties =
                await session.TryGetMediaPropertiesAsync();
            MediaTrackSnapshot snapshot = MediaTrackText.Normalize(new MediaTrackSnapshot(
                ProbeSucceeded: true,
                SessionFound: true,
                properties?.Title ?? string.Empty,
                properties?.Artist ?? string.Empty));
            return SupplementArtistFromWindow(snapshot, ReadFromWindowTitle(targetProcessName));
        }
        catch (Exception)
        {
            _sessionManager = null;
            MediaTrackSnapshot fallback = ReadFromWindowTitle(targetProcessName);
            return fallback.HasTrack ? fallback : MediaTrackSnapshot.Unavailable;
        }
    }

    internal static MediaTrackSnapshot SupplementArtistFromWindow(
        MediaTrackSnapshot media,
        MediaTrackSnapshot window)
    {
        if (!media.HasTrack)
        {
            return window;
        }

        // Some CloudMusic versions publish only the first collaborator to SMTC.
        // Supplement only the same track and a compatible artist list; a stale
        // window during a track switch must not replace the current metadata.
        if (window.ProbeSucceeded && window.HasTrack &&
            string.Equals(media.Title, window.Title, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(window.Artist) &&
            (string.IsNullOrWhiteSpace(media.Artist) ||
                window.Artist.IndexOf(media.Artist, StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return media with { Artist = window.Artist };
        }

        return media;
    }

    internal static MediaTrackSnapshot ReadFromWindowTitle(string targetProcessName)
    {
        string processName = Path.GetFileNameWithoutExtension(targetProcessName.Trim());
        if (string.IsNullOrWhiteSpace(processName))
        {
            return MediaTrackSnapshot.NoSession;
        }

        try
        {
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    if (TryParseWindowTitle(process.MainWindowTitle, out string title, out string artist))
                    {
                        return MediaTrackText.Normalize(new MediaTrackSnapshot(
                            ProbeSucceeded: true,
                            SessionFound: true,
                            title,
                            artist));
                    }
                }
            }

            return MediaTrackSnapshot.NoSession;
        }
        catch (Exception)
        {
            return MediaTrackSnapshot.Unavailable;
        }
    }

    internal static bool TryParseWindowTitle(
        string? windowTitle,
        out string title,
        out string artist)
    {
        title = string.Empty;
        artist = string.Empty;
        if (string.IsNullOrWhiteSpace(windowTitle))
        {
            return false;
        }

        string candidate = windowTitle!.Trim();
        string[] genericTitles = ["网易云音乐", "NetEase CloudMusic", "CloudMusic"];
        if (genericTitles.Contains(candidate, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (string genericTitle in genericTitles)
        {
            string suffix = $" - {genericTitle}";
            if (candidate.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate.Substring(0, candidate.Length - suffix.Length).Trim();
                break;
            }
        }

        int separator = candidate.LastIndexOf(" - ", StringComparison.Ordinal);
        if (separator > 0 && separator < candidate.Length - 3)
        {
            title = candidate.Substring(0, separator).Trim();
            artist = candidate.Substring(separator + 3).Trim();
        }
        else
        {
            title = candidate;
        }

        return !string.IsNullOrWhiteSpace(title);
    }

    internal static bool MatchesSourceApplication(string? sourceAppUserModelId, string targetProcessName)
    {
        if (string.IsNullOrWhiteSpace(sourceAppUserModelId) ||
            string.IsNullOrWhiteSpace(targetProcessName))
        {
            return false;
        }

        string targetFileName = Path.GetFileName(targetProcessName.Trim());
        string targetStem = Path.GetFileNameWithoutExtension(targetFileName);
        string source = sourceAppUserModelId!.Trim();
        if (source.Equals(targetFileName, StringComparison.OrdinalIgnoreCase) ||
            source.Equals(targetStem, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string sourceFileName = Path.GetFileName(source);
        if (sourceFileName.Equals(targetFileName, StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileNameWithoutExtension(sourceFileName).Equals(
                targetStem,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        char[] separators = ['.', '!', '_', '-', '/', '\\', ':'];
        return source
            .Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Any(part => part.Equals(targetStem, StringComparison.OrdinalIgnoreCase));
    }
}
