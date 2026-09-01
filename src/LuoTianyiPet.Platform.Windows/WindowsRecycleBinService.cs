using System.Runtime.InteropServices;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.Platform.Windows;

public sealed class WindowsRecycleBinService : IRecycleBinService
{
    private const int MaximumItemsPerDrop = 100;
    private static readonly Guid FileOperationClassId =
        new("3AD05575-8857-4850-9277-11B85BDB8E09");

    public Task<RecycleBinOperationResult> MoveToRecycleBinAsync(
        IReadOnlyList<string> paths,
        nint ownerWindowHandle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        RecycleBinOperationResult? rejected = Validate(paths);
        if (rejected is not null)
        {
            return Task.FromResult(rejected);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<RecycleBinOperationResult>(cancellationToken);
        }

        string[] normalizedPaths = paths
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        TaskCompletionSource<RecycleBinOperationResult> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Thread operationThread = new(() =>
        {
            try
            {
                completion.TrySetResult(MoveCore(normalizedPaths, ownerWindowHandle));
            }
            catch (Exception exception)
            {
                completion.TrySetResult(new RecycleBinOperationResult(
                    RecycleBinOperationStatus.Failed,
                    normalizedPaths.Length,
                    CountMissing(normalizedPaths),
                    exception.GetType().Name));
            }
        })
        {
            IsBackground = true,
            Name = "LuoTianyiPet.RecycleBin",
        };
        operationThread.SetApartmentState(ApartmentState.STA);
        operationThread.Start();
        return completion.Task;
    }

    private static RecycleBinOperationResult? Validate(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return Rejected(0, "没有收到可回收的本地文件。");
        }

        if (paths.Count > MaximumItemsPerDrop)
        {
            return Rejected(paths.Count, $"一次最多接收 {MaximumItemsPerDrop} 个项目。");
        }

        HashSet<string> uniquePaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (string path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            {
                return Rejected(paths.Count, "只接受具有完整路径的本地文件或文件夹。");
            }

            string normalizedPath;
            try
            {
                normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return Rejected(paths.Count, "文件路径无效，未执行任何操作。");
            }

            if (!uniquePaths.Add(normalizedPath))
            {
                continue;
            }

            if (normalizedPath.StartsWith("\\\\", StringComparison.Ordinal) ||
                IsDriveRoot(normalizedPath))
            {
                return Rejected(paths.Count, "不接受网络位置或磁盘根目录。");
            }

            if (!File.Exists(normalizedPath) && !Directory.Exists(normalizedPath))
            {
                return Rejected(paths.Count, "至少有一个项目已经不存在，未执行任何操作。");
            }
        }

        return null;
    }

    private static RecycleBinOperationResult MoveCore(string[] paths, nint ownerWindowHandle)
    {
        Type operationType = Type.GetTypeFromCLSID(FileOperationClassId, throwOnError: true)!;
        IFileOperationNative operation = (IFileOperationNative)(
            Activator.CreateInstance(operationType) ??
            throw new COMException("Windows 文件操作服务不可用。"));
        List<IShellItemNative> shellItems = [];
        try
        {
            ThrowIfFailed(operation.SetOwnerWindow(ownerWindowHandle));
            ThrowIfFailed(operation.SetOperationFlags(
                FileOperationFlags.AllowUndo |
                FileOperationFlags.NoConfirmMakeDirectory |
                FileOperationFlags.RecycleOnDelete));

            foreach (string path in paths)
            {
                Guid shellItemId = typeof(IShellItemNative).GUID;
                ThrowIfFailed(SHCreateItemFromParsingName(
                    path,
                    nint.Zero,
                    ref shellItemId,
                    out IShellItemNative shellItem));
                shellItems.Add(shellItem);
                ThrowIfFailed(operation.DeleteItem(shellItem, nint.Zero));
            }

            ThrowIfFailed(operation.PerformOperations());
            ThrowIfFailed(operation.GetAnyOperationsAborted(out bool aborted));
            int recycledCount = CountMissing(paths);
            if (recycledCount == paths.Length && !aborted)
            {
                return new RecycleBinOperationResult(
                    RecycleBinOperationStatus.Success,
                    paths.Length,
                    recycledCount,
                    "项目已移入 Windows 回收站。");
            }

            if (recycledCount > 0)
            {
                return new RecycleBinOperationResult(
                    RecycleBinOperationStatus.PartialFailure,
                    paths.Length,
                    recycledCount,
                    "只有部分项目进入了回收站，其余项目仍在原处。");
            }

            return new RecycleBinOperationResult(
                aborted ? RecycleBinOperationStatus.Cancelled : RecycleBinOperationStatus.Failed,
                paths.Length,
                0,
                aborted ? "回收操作已取消，文件仍在原处。" : "Windows 没有将项目移入回收站。");
        }
        finally
        {
            foreach (IShellItemNative shellItem in shellItems)
            {
                if (Marshal.IsComObject(shellItem))
                {
                    Marshal.FinalReleaseComObject(shellItem);
                }
            }

            if (Marshal.IsComObject(operation))
            {
                Marshal.FinalReleaseComObject(operation);
            }
        }
    }

    private static bool IsDriveRoot(string path)
    {
        string? root = Path.GetPathRoot(path);
        return root is not null &&
            Path.TrimEndingDirectorySeparator(root).Equals(path, StringComparison.OrdinalIgnoreCase);
    }

    private static int CountMissing(IEnumerable<string> paths) =>
        paths.Count(path => !File.Exists(path) && !Directory.Exists(path));

    private static RecycleBinOperationResult Rejected(int requestedCount, string message) => new(
        RecycleBinOperationStatus.Rejected,
        requestedCount,
        0,
        message);

    private static void ThrowIfFailed(int hresult) => Marshal.ThrowExceptionForHR(hresult);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        string path,
        nint bindingContext,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemNative shellItem);

    [Flags]
    private enum FileOperationFlags : uint
    {
        AllowUndo = 0x00000040,
        NoConfirmMakeDirectory = 0x00000200,
        RecycleOnDelete = 0x00080000,
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemNative
    {
        void BindToHandler();
        void GetParent();
        void GetDisplayName();
        void GetAttributes();
        void Compare();
    }

    [ComImport]
    [Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperationNative
    {
        [PreserveSig] int Advise(nint progressSink, out uint cookie);
        [PreserveSig] int Unadvise(uint cookie);
        [PreserveSig] int SetOperationFlags(FileOperationFlags operationFlags);
        [PreserveSig] int SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
        [PreserveSig] int SetProgressDialog(nint operationsProgressDialog);
        [PreserveSig] int SetProperties(nint propertyChangeArray);
        [PreserveSig] int SetOwnerWindow(nint ownerWindow);
        [PreserveSig] int ApplyPropertiesToItem(nint item);
        [PreserveSig] int ApplyPropertiesToItems(nint items);
        [PreserveSig] int RenameItem(nint item, [MarshalAs(UnmanagedType.LPWStr)] string newName, nint progressSink);
        [PreserveSig] int RenameItems(nint items, [MarshalAs(UnmanagedType.LPWStr)] string newName);
        [PreserveSig] int MoveItem(nint item, nint destinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string newName, nint progressSink);
        [PreserveSig] int MoveItems(nint items, nint destinationFolder);
        [PreserveSig] int CopyItem(nint item, nint destinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string copyName, nint progressSink);
        [PreserveSig] int CopyItems(nint items, nint destinationFolder);
        [PreserveSig] int DeleteItem([MarshalAs(UnmanagedType.Interface)] IShellItemNative item, nint progressSink);
        [PreserveSig] int DeleteItems(nint items);
        [PreserveSig] int NewItem(nint destinationFolder, uint fileAttributes, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string templateName, nint progressSink);
        [PreserveSig] int PerformOperations();
        [PreserveSig] int GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }
}
