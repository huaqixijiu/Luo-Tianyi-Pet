using LuoTianyiPet.Core;

namespace LuoTianyiPet.Platform.Windows.Tests;

public sealed class WindowsRecycleBinServiceTests
{
    [Fact]
    public async Task EmptyDropIsRejectedWithoutCallingTheShell()
    {
        WindowsRecycleBinService service = new();

        RecycleBinOperationResult result = await service.MoveToRecycleBinAsync([], nint.Zero);

        Assert.Equal(RecycleBinOperationStatus.Rejected, result.Status);
        Assert.Equal(0, result.RecycledCount);
    }

    [Fact]
    public async Task RelativePathIsRejectedWithoutCallingTheShell()
    {
        WindowsRecycleBinService service = new();

        RecycleBinOperationResult result = await service.MoveToRecycleBinAsync(
            ["relative-file.txt"],
            nint.Zero);

        Assert.Equal(RecycleBinOperationStatus.Rejected, result.Status);
        Assert.Contains("完整路径", result.Message);
    }

    [Fact]
    public async Task MissingLocalPathIsRejectedWithoutCallingTheShell()
    {
        WindowsRecycleBinService service = new();
        string missing = Path.Combine(
            Path.GetTempPath(),
            $"luotianyi-pet-missing-{Guid.NewGuid():N}.txt");

        RecycleBinOperationResult result = await service.MoveToRecycleBinAsync(
            [missing],
            nint.Zero);

        Assert.Equal(RecycleBinOperationStatus.Rejected, result.Status);
        Assert.Contains("不存在", result.Message);
    }

    [Fact]
    public async Task NetworkPathIsRejectedWithoutCallingTheShell()
    {
        WindowsRecycleBinService service = new();

        RecycleBinOperationResult result = await service.MoveToRecycleBinAsync(
            [@"\\server\share\file.txt"],
            nint.Zero);

        Assert.Equal(RecycleBinOperationStatus.Rejected, result.Status);
        Assert.Contains("网络位置", result.Message);
    }

}
