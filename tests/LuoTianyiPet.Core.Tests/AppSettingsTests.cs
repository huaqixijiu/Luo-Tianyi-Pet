using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void Defaults_AreSafeAndNotTopmost()
    {
        AppSettings settings = new();

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.False(settings.Window.AlwaysOnTop);
        Assert.Null(settings.Window.Left);
        Assert.Null(settings.Window.Top);
    }
}
