namespace LuoTianyiPet.Platform.Windows.Tests;

public sealed class SingleInstanceGuardTests
{
    [Fact]
    public void Acquire_AllowsOnlyOnePrimaryInstanceForTheSameId()
    {
        string applicationId = $"LuoTianyiPet.Tests.{Guid.NewGuid():N}";

        using SingleInstanceGuard first = SingleInstanceGuard.Acquire(applicationId);
        using SingleInstanceGuard second = SingleInstanceGuard.Acquire(applicationId);

        Assert.True(first.IsPrimaryInstance);
        Assert.False(second.IsPrimaryInstance);
    }
}
