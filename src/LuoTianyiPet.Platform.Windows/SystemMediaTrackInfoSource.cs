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
            return snapshot.HasTrack
                ? snapshot
                : ReadFromWindowTitle(targetProcessName);
        }
        catch (Exception)
        {
            _sessionManager = null;
            MediaTrackSnapshot fallback = ReadFromWindowTitle(targetProcessName);
            return fallback.HasTrack ? fallback : MediaTrackSnapshot.Unavailable;
        }
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

        string candidate = windowTitle.Trim();
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
                candidate = candidate[..^suffix.Length].Trim();
                break;
            }
        }

        int separator = candidate.LastIndexOf(" - ", StringComparison.Ordinal);
        if (separator > 0 && separator < candidate.Length - 3)
        {
            title = candidate[..separator].Trim();
            artist = candidate[(separator + 3)..].Trim();
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
        string source = sourceAppUserModelId.Trim();
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
