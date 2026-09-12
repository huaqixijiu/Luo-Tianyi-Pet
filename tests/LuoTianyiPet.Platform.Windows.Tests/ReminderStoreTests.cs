using LuoTianyiPet.Core;

namespace LuoTianyiPet.Platform.Windows.Tests;

public class ReminderStoreTests
{
    [Fact]
    public async Task BackupRecoversCorruptionAndPreservesDamagedFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "LuoReminderTest-" + Guid.NewGuid());
        try
        {
            ReminderStore store = new(new LocalAppPaths(dir));
            ReminderBook b = new() { Items = [new() { Title = "测试标题", Notes = "多行\n内容", Start = new DateTime(2026, 9, 14) }] };
            await store.SaveAsync(b);
            b.Items[0].Title = "修改"; await store.SaveAsync(b);
            File.WriteAllText(Path.Combine(dir, "reminders.json"), "broken");
            ReminderBook restored = await store.LoadAsync();
            Assert.Equal("测试标题", restored.Items[0].Title);
            Assert.Single(Directory.GetFiles(dir, "*.damaged-*"));
            Assert.NotNull(store.RecoveryMessage);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
    [Fact]
    public async Task InvalidImportCannotOverwriteValidFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "LuoReminderTest-" + Guid.NewGuid());
        try
        {
            ReminderStore store = new(new LocalAppPaths(dir));
            await store.SaveAsync(new ReminderBook());
            await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(new ReminderBook { Version = 999 }));
            Assert.Equal(1, (await store.LoadAsync()).Version);
            Assert.Throws<ArgumentException>(() => ReminderStore.Decode("{\"Version\":1,\"Items\":[null]}"));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
