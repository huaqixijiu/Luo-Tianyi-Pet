using System.Text.Json;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.Platform.Windows;

public sealed class ReminderStore
{
    private readonly string _file;
    public string? RecoveryMessage { get; private set; }
    public ReminderStore(LocalAppPaths paths) => _file = Path.Combine(paths.RootDirectory, "reminders.json");
    public static string Encode(ReminderBook book) => JsonSerializer.Serialize(book);
    public static ReminderBook Decode(string json)
    {
        var book = JsonSerializer.Deserialize<ReminderBook>(json) ?? throw new ArgumentException("数据为空。");
        ReminderSchedule.Validate(book);
        return book;
    }
    public async Task<ReminderBook> LoadAsync() => await Task.Run(() =>
    {
        if (!File.Exists(_file)) return new ReminderBook();
        try { return Read(_file); }
        catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or UnauthorizedAccessException)
        {
            string backup = BackupFile;
            if (!File.Exists(backup)) throw new IOException("提醒数据无法读取；原文件已保留，请先导出检查。", ex);
            ReminderBook restored = Read(backup);
            File.Copy(_file, _file + ".damaged-" + DateTime.UtcNow.Ticks);
            File.Copy(backup, _file, true);
            RecoveryMessage = "提醒数据已从上次备份恢复，异常文件已保留。";
            return restored;
        }
    }).ConfigureAwait(false);

    private string BackupFile => Path.Combine(Path.GetDirectoryName(_file)!, "reminders.backup.json");
    public static ReminderBook Read(string path)
    {
        if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new IOException("提醒文件超过 16 MB。");
        return Decode(File.ReadAllText(path));
    }
    public async Task SaveAsync(ReminderBook book)
    {
        ReminderSchedule.Validate(book);
        string json = Encode(book);
        await Task.Run(() =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            string temp = _file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, json);
                if (File.Exists(_file)) File.Replace(temp, _file, BackupFile);
                else File.Move(temp, _file);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }).ConfigureAwait(false);
    }
}
