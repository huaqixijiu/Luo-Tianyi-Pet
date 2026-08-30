using System.Diagnostics;
using System.Runtime.InteropServices;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.Platform.Windows;

public readonly record struct ForegroundProcessQuery(bool Succeeded, string? ProcessName);

public readonly record struct ShortcutKeyStroke(ushort VirtualKey, bool IsKeyUp);

public interface IShortcutInputBackend
{
    ForegroundProcessQuery QueryForegroundProcess();

    bool IsKeyDown(ushort virtualKey);

    int Send(IReadOnlyList<ShortcutKeyStroke> strokes);
}

public sealed class WindowsMediaCommandSender : IMediaCommandSender
{
    private static readonly ushort[] BusyStateKeys =
    [
        VirtualKeys.Shift,
        VirtualKeys.Control,
        VirtualKeys.Menu,
        VirtualKeys.LeftWindows,
        VirtualKeys.RightWindows,
    ];

    private readonly IShortcutInputBackend _backend;
    private readonly bool _enabled;
    private readonly IReadOnlyDictionary<MediaCommand, ShortcutBinding> _bindings;
    private readonly HashSet<string> _protectedProcesses;
    private readonly TimeSpan _cooldown;
    private DateTimeOffset? _lastSentAt;

    public WindowsMediaCommandSender(
        IShortcutInputBackend backend,
        MediaPreferences mediaPreferences,
        SafetyPreferences safetyPreferences)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(mediaPreferences);
        ArgumentNullException.ThrowIfNull(safetyPreferences);

        _backend = backend;
        _enabled = mediaPreferences.EnableCloudMusicShortcutControl;
        _bindings = new Dictionary<MediaCommand, ShortcutBinding>
        {
            [MediaCommand.PreviousTrack] = ShortcutBinding.Parse(mediaPreferences.PreviousTrackShortcut),
            [MediaCommand.TogglePlayPause] = ShortcutBinding.Parse(mediaPreferences.TogglePlayPauseShortcut),
            [MediaCommand.NextTrack] = ShortcutBinding.Parse(mediaPreferences.NextTrackShortcut),
            [MediaCommand.FavoriteTrack] = ShortcutBinding.Parse(mediaPreferences.FavoriteTrackShortcut),
        };
        _protectedProcesses = (safetyPreferences.ProtectedForegroundProcessNames ?? string.Empty)
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(NormalizeProcessName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _cooldown = TimeSpan.FromMilliseconds(Math.Max(0, mediaPreferences.CommandCooldownMilliseconds));
    }

    public MediaCommandSendResult TrySend(MediaCommand command, DateTimeOffset now)
    {
        if (!_enabled)
        {
            return new(MediaCommandSendStatus.Disabled);
        }

        if (!_bindings.TryGetValue(command, out ShortcutBinding? binding))
        {
            return new(MediaCommandSendStatus.InvalidShortcut);
        }

        if (!binding.IsValid)
        {
            return new(MediaCommandSendStatus.InvalidShortcut);
        }

        ForegroundProcessQuery foreground = _backend.QueryForegroundProcess();
        if (!foreground.Succeeded)
        {
            return new(MediaCommandSendStatus.ForegroundCheckUnavailable);
        }

        if (foreground.ProcessName is string processName &&
            _protectedProcesses.Contains(NormalizeProcessName(processName)))
        {
            return new(MediaCommandSendStatus.ProtectedApplicationForeground);
        }

        if (_lastSentAt is DateTimeOffset lastSentAt && now - lastSentAt < _cooldown)
        {
            return new(MediaCommandSendStatus.RateLimited);
        }

        if (BusyStateKeys.Any(_backend.IsKeyDown) || binding.Keys.Any(_backend.IsKeyDown))
        {
            return new(MediaCommandSendStatus.KeyboardBusy);
        }

        IReadOnlyList<ShortcutKeyStroke> strokes = binding.CreateStrokes();
        int sentCount = _backend.Send(strokes);
        if (sentCount != strokes.Count)
        {
            if (sentCount > 0)
            {
                _backend.Send(binding.CreateReleaseStrokes());
            }

            return new(MediaCommandSendStatus.SystemRejected);
        }

        _lastSentAt = now;
        return new(MediaCommandSendStatus.Sent);
    }

    private static string NormalizeProcessName(string processName) =>
        Path.GetFileNameWithoutExtension(processName.Trim());
}

public sealed class Win32ShortcutInputBackend : IShortcutInputBackend
{
    public ForegroundProcessQuery QueryForegroundProcess()
    {
        nint window = NativeMethods.GetForegroundWindow();
        if (window == 0)
        {
            return new(true, null);
        }

        if (NativeMethods.GetWindowThreadProcessId(window, out uint processId) == 0 || processId == 0)
        {
            return new(false, null);
        }

        try
        {
            using Process process = Process.GetProcessById(checked((int)processId));
            return new(true, process.ProcessName);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new(false, null);
        }
    }

    public bool IsKeyDown(ushort virtualKey) => NativeMethods.GetAsyncKeyState(virtualKey) < 0;

    public int Send(IReadOnlyList<ShortcutKeyStroke> strokes)
    {
        Input[] inputs = strokes
            .Select(stroke => new Input
            {
                Type = NativeMethods.InputKeyboard,
                Union = new InputUnion
                {
                    Keyboard = new KeyboardInput
                    {
                        VirtualKey = stroke.VirtualKey,
                        Flags = stroke.IsKeyUp ? NativeMethods.KeyEventKeyUp : 0,
                    },
                },
            })
            .ToArray();

        return checked((int)NativeMethods.SendInput(
            checked((uint)inputs.Length),
            inputs,
            Marshal.SizeOf<Input>()));
    }
}

internal sealed record ShortcutBinding(bool IsValid, IReadOnlyList<ushort> Modifiers, ushort PrimaryKey)
{
    public IEnumerable<ushort> Keys => Modifiers.Append(PrimaryKey);

    public static ShortcutBinding Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Invalid();
        }

        string[] parts = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return Invalid();
        }

        List<ushort> modifiers = [];
        ushort? primaryKey = null;
        foreach (string part in parts)
        {
            if (TryParseModifier(part, out ushort modifier))
            {
                if (!modifiers.Contains(modifier))
                {
                    modifiers.Add(modifier);
                }

                continue;
            }

            if (primaryKey is not null || !TryParsePrimaryKey(part, out ushort key))
            {
                return Invalid();
            }

            primaryKey = key;
        }

        return modifiers.Count > 0 && primaryKey is not null
            ? new(true, modifiers, primaryKey.Value)
            : Invalid();
    }

    public IReadOnlyList<ShortcutKeyStroke> CreateStrokes()
    {
        List<ShortcutKeyStroke> strokes = [];
        strokes.AddRange(Modifiers.Select(key => new ShortcutKeyStroke(key, false)));
        strokes.Add(new(PrimaryKey, false));
        strokes.Add(new(PrimaryKey, true));
        strokes.AddRange(Modifiers.Reverse().Select(key => new ShortcutKeyStroke(key, true)));
        return strokes;
    }

    public IReadOnlyList<ShortcutKeyStroke> CreateReleaseStrokes() =>
        [
            new(PrimaryKey, true),
            .. Modifiers.Reverse().Select(key => new ShortcutKeyStroke(key, true)),
        ];

    private static bool TryParseModifier(string value, out ushort key)
    {
        key = value.ToUpperInvariant() switch
        {
            "CTRL" or "CONTROL" => VirtualKeys.Control,
            "ALT" => VirtualKeys.Menu,
            "SHIFT" => VirtualKeys.Shift,
            _ => 0,
        };
        return key != 0;
    }

    private static bool TryParsePrimaryKey(string value, out ushort key)
    {
        string normalized = value.ToUpperInvariant();
        if (normalized.Length == 1 && normalized[0] is >= 'A' and <= 'Z' or >= '0' and <= '9')
        {
            key = normalized[0];
            return true;
        }

        key = normalized switch
        {
            "LEFT" => VirtualKeys.Left,
            "RIGHT" => VirtualKeys.Right,
            "UP" => VirtualKeys.Up,
            "DOWN" => VirtualKeys.Down,
            "SPACE" => VirtualKeys.Space,
            _ => 0,
        };
        return key != 0;
    }

    private static ShortcutBinding Invalid() => new(false, [], 0);
}

internal static class VirtualKeys
{
    public const ushort Shift = 0x10;
    public const ushort Control = 0x11;
    public const ushort Menu = 0x12;
    public const ushort Space = 0x20;
    public const ushort Left = 0x25;
    public const ushort Up = 0x26;
    public const ushort Right = 0x27;
    public const ushort Down = 0x28;
    public const ushort LeftWindows = 0x5B;
    public const ushort RightWindows = 0x5C;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Input
{
    public uint Type;
    public InputUnion Union;
}

[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    [FieldOffset(0)]
    public KeyboardInput Keyboard;

    [FieldOffset(0)]
    public MouseInput Mouse;

    [FieldOffset(0)]
    public HardwareInput Hardware;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KeyboardInput
{
    public ushort VirtualKey;
    public ushort ScanCode;
    public uint Flags;
    public uint Time;
    public nuint ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MouseInput
{
    public int X;
    public int Y;
    public uint MouseData;
    public uint Flags;
    public uint Time;
    public nuint ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HardwareInput
{
    public uint Message;
    public ushort ParameterLow;
    public ushort ParameterHigh;
}

internal static class NativeMethods
{
    public const uint InputKeyboard = 1;
    public const uint KeyEventKeyUp = 0x0002;

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);
}
