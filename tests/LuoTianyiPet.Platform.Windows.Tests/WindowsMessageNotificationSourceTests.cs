using LuoTianyiPet.Core;

namespace LuoTianyiPet.Platform.Windows.Tests;

public sealed class WindowsMessageNotificationSourceTests
{
    [Fact]
    public async Task UnpackagedTestHostFailsClosedWithoutRequestingPermission()
    {
        MessageProviderMatcher matcher = new(new MessageNotificationPreferences());
        using WindowsMessageNotificationSource source = new(matcher);

        Assert.False(WindowsPackageIdentity.HasCurrentPackageIdentity());
        Assert.Equal(
            MessageNotificationAccessStatus.PackageIdentityRequired,
            source.GetAccessStatus());
        Assert.Equal(
            MessageNotificationAccessStatus.PackageIdentityRequired,
            await source.RequestAccessAsync());

        source.Start();
        source.Stop();
    }
}
