using System.Text.Json;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.Platform.Windows;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly LocalAppPaths _paths;

    public JsonSettingsStore(LocalAppPaths paths)
    {
        _paths = paths;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_paths.SettingsFile))
        {
            return new AppSettings();
        }

        try
        {
            string json = await File.ReadAllTextAsync(_paths.SettingsFile, cancellationToken).ConfigureAwait(false);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
            return settings?.SchemaVersion switch
            {
                AppSettings.CurrentSchemaVersion => Normalize(settings),
                1 => MigrateFromPreviousVersion(settings, 1500),
                2 => MigrateFromPreviousVersion(settings, 500),
                3 => MigrateFromPreviousVersion(
                    settings,
                    MediaPreferences.DefaultSilenceGraceMilliseconds),
                4 => MigrateFromPreviousVersion(
                    settings,
                    MediaPreferences.DefaultSilenceGraceMilliseconds),
                5 => MigrateFromPreviousVersion(
                    settings,
                    MediaPreferences.DefaultSilenceGraceMilliseconds),
                6 => MigrateFromPreviousVersion(
                    settings,
                    MediaPreferences.DefaultSilenceGraceMilliseconds),
                7 => MigrateFromPreviousVersion(
                    settings,
                    MediaPreferences.DefaultSilenceGraceMilliseconds),
                _ => new AppSettings(),
            };
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            TryPreserveCorruptSettings();
            return new AppSettings();
        }
    }

    private static AppSettings Normalize(AppSettings settings) => settings with
    {
        Window = settings.Window ?? new WindowPreferences(),
        Media = settings.Media ?? new MediaPreferences(),
        Volume = settings.Volume ?? new VolumePreferences(),
        Genshin = settings.Genshin ?? new GenshinPreferences(),
        Notifications = settings.Notifications ?? new MessageNotificationPreferences(),
        Safety = settings.Safety ?? new SafetyPreferences(),
    };

    private static AppSettings MigrateFromPreviousVersion(
        AppSettings settings,
        int previousDefaultGraceMilliseconds)
    {
        MediaPreferences media = settings.Media ?? new MediaPreferences();
        if (media.SilenceGraceMilliseconds == previousDefaultGraceMilliseconds)
        {
            media = media with
            {
                SilenceGraceMilliseconds = MediaPreferences.DefaultSilenceGraceMilliseconds,
            };
        }

        VolumePreferences volume = settings.Volume ?? new VolumePreferences();
        if (volume.MergeChangesWithinMilliseconds == 500)
        {
            volume = volume with
            {
                MergeChangesWithinMilliseconds =
                    VolumePreferences.DefaultMergeChangesWithinMilliseconds,
            };
        }

        return settings with
        {
            SchemaVersion = AppSettings.CurrentSchemaVersion,
            Window = settings.Window ?? new WindowPreferences(),
            Media = media,
            Volume = volume,
            Genshin = settings.Genshin ?? new GenshinPreferences(),
            Notifications = settings.Notifications ?? new MessageNotificationPreferences(),
            Safety = settings.Safety ?? new SafetyPreferences(),
        };
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(_paths.RootDirectory);
        string temporaryFile = Path.Combine(
            _paths.RootDirectory,
            $"settings-{Guid.NewGuid():N}.tmp");

        string json = JsonSerializer.Serialize(settings, SerializerOptions);
        await File.WriteAllTextAsync(temporaryFile, json, cancellationToken).ConfigureAwait(false);
        File.Move(temporaryFile, _paths.SettingsFile, overwrite: true);
    }

    private void TryPreserveCorruptSettings()
    {
        try
        {
            if (!File.Exists(_paths.SettingsFile))
            {
                return;
            }

            string backupFile = Path.Combine(
                _paths.RootDirectory,
                $"settings.corrupt-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json");
            File.Move(_paths.SettingsFile, backupFile, overwrite: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Settings failures must never prevent the pet from starting with safe defaults.
        }
    }
}
