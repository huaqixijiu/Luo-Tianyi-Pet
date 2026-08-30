namespace LuoTianyiPet.Core;

public enum StartupRegistrationStatus
{
    Succeeded,
    Unchanged,
    Unavailable,
    Rejected,
}

public sealed record StartupRegistrationResult(
    StartupRegistrationStatus Status,
    bool IsEnabled);

public interface IStartupRegistrationService
{
    bool IsEnabled { get; }

    StartupRegistrationResult TrySetEnabled(bool enabled);
}
