using System.Diagnostics;
namespace LuoTianyiPet.Platform.Windows.Tests;

public sealed class DesktopObserverStartupTests
{
    [Fact]
    public void SlowExplorerCannotBlockStartupOrDisposal()
    {
        using ManualResetEventSlim entered=new(false),release=new(false);
        using var source=new WindowsDesktopItemDisappearanceSource(()=>{entered.Set();release.Wait(TimeSpan.FromSeconds(4));});
        try
        {
            var clock=Stopwatch.StartNew();
            source.Start();
            Assert.True(clock.Elapsed<TimeSpan.FromSeconds(1));
            Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
            clock.Restart();source.Dispose();
            Assert.True(clock.Elapsed<TimeSpan.FromSeconds(1));
        }
        finally { release.Set(); }
    }
}
