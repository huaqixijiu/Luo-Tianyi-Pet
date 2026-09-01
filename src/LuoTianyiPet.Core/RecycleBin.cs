namespace LuoTianyiPet.Core;

public enum RecycleBinOperationStatus
{
    Success,
    Rejected,
    Cancelled,
    Failed,
    PartialFailure,
}

public sealed record RecycleBinOperationResult(
    RecycleBinOperationStatus Status,
    int RequestedCount,
    int RecycledCount,
    string Message)
{
    public bool Succeeded => Status == RecycleBinOperationStatus.Success;
}

public interface IRecycleBinService
{
    Task<RecycleBinOperationResult> MoveToRecycleBinAsync(
        IReadOnlyList<string> paths,
        nint ownerWindowHandle,
        CancellationToken cancellationToken = default);
}
