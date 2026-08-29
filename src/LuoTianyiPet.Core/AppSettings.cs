namespace LuoTianyiPet.Core;

public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public WindowPreferences Window { get; init; } = new();
}

public sealed record WindowPreferences
{
    public bool AlwaysOnTop { get; init; }

    public double? Left { get; init; }

    public double? Top { get; init; }
}
