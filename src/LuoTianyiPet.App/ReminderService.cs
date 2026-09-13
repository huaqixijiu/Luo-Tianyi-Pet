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
        await ChangeAsync(b => ReminderEngine.Initialize(b, DateTime.Now));
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
            ReminderEngine.Reconcile(next);
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
        if (ReminderEngine.Advance(check, DateTime.Now))
            await ChangeAsync(book => ReminderEngine.Advance(book, DateTime.Now));
    }
    private void Schedule()
    {
        _timer.Stop();
        DateTime now = DateTime.Now;
        DateTime? next = Book.Items.Where(ReminderEngine.Active).Select(i => {
            var at=ReminderSchedule.Next(i,Book,i.CheckedThrough);
            return at is DateTime due && i.EarlyEnabled==true && due.AddMinutes(-i.EarlyMinutes)>i.CheckedThrough ? due.AddMinutes(-i.EarlyMinutes) : at;
        }).Concat(Book.Occurrences.Where(o=>o.Phase is ReminderPhase.Early or ReminderPhase.EarlySnoozed or ReminderPhase.AcknowledgedEarly or ReminderPhase.DueSnoozed).Select(o=>o.SnoozeAt ?? (DateTime?)o.At))
        .Concat(Book.Occurrences.Where(o=>o.Phase is ReminderPhase.Early or ReminderPhase.Due && o.RoundStartedAt!=null && (Book.Preferences.Sound||Book.Preferences.Animation)).Select(o=>(DateTime?)o.RoundStartedAt!.Value.AddSeconds(ReminderEngine.MaximumRoundSeconds)))
        .Where(t=>t!=null).OrderBy(t=>t).FirstOrDefault();
        // A bounded watchdog catches system clock changes/resume without high-frequency polling.
        _timer.Interval = TimeSpan.FromSeconds(next == null ? 60 : Math.Max(0.1, Math.Min(60, (next.Value - now).TotalSeconds)));
        _timer.Start();
    }
    public void Dispose() { _timer.Stop(); _timer.Tick -= OnTick; }
}
