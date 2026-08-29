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
            string json = await File.ReadAllTextAsync(_paths.SettingsFile, cancellationToken);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
            return settings is { SchemaVersion: AppSettings.CurrentSchemaVersion }
                ? settings
                : new AppSettings();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            TryPreserveCorruptSettings();
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(_paths.RootDirectory);
        string temporaryFile = Path.Combine(
            _paths.RootDirectory,
            $"settings-{Guid.NewGuid():N}.tmp");

        string json = JsonSerializer.Serialize(settings, SerializerOptions);
        await File.WriteAllTextAsync(temporaryFile, json, cancellationToken);
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
