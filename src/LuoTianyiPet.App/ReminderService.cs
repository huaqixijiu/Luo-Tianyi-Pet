using System.Windows.Threading;
using LuoTianyiPet.Core;
using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.App;

internal sealed class ReminderService : IDisposable
{
    private readonly ReminderStore _store;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background);
    public ReminderBook Book { get; private set; } = new();
    public event Action? Changed;
    public event Action<string>? Failed;
    public ReminderService(LocalAppPaths paths)
    {
        _store = new(paths);
        _timer.Tick += OnTick;
    }
    public async Task LoadAsync()
    {
        Book = await _store.LoadAsync();
        await CheckAsync();
        if (_store.RecoveryMessage != null) Failed?.Invoke(_store.RecoveryMessage);
        Schedule();
    }
    public async Task ChangeAsync(Action<ReminderBook> change)
    {
        await _gate.WaitAsync();
        try
        {
            ReminderBook next = ReminderStore.Decode(ReminderStore.Encode(Book));
            change(next);
            ReminderSchedule.Validate(next);
            await _store.SaveAsync(next);
            Book = next;
            Changed?.Invoke();
        }
        finally { _gate.Release(); Schedule(); }
    }
    private async void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        try { await CheckAsync(); }
        catch { Failed?.Invoke("提醒数据保存失败，待处理事项已保留，将稍后重试。"); }
        finally { Schedule(); }
    }
    private async Task CheckAsync()
    {
        // The check operates on a clone: failed writes never silently advance the live cursor.
        ReminderBook check = ReminderStore.Decode(ReminderStore.Encode(Book));
        if (ReminderSchedule.Advance(check, DateTime.Now))
            await ChangeAsync(book => ReminderSchedule.Advance(book, DateTime.Now));
    }
    private void Schedule()
    {
        _timer.Stop();
        DateTime now = DateTime.Now;
        DateTime? next = Book.Items.Where(i => i.PendingAt == null)
            .Select(i => ReminderSchedule.Next(i, Book, i.CheckedThrough)).Where(t => t != null).OrderBy(t => t).FirstOrDefault();
        // A bounded watchdog catches system clock changes/resume without high-frequency polling.
        _timer.Interval = TimeSpan.FromSeconds(next == null ? 60 : Math.Max(0.1, Math.Min(60, (next.Value - now).TotalSeconds)));
        _timer.Start();
    }
    public void Dispose() { _timer.Stop(); _timer.Tick -= OnTick; }
}
