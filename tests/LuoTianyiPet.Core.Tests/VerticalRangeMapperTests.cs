using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class VerticalRangeMapperTests
{
    [Theory]
    [InlineData(9, 100)]
    [InlineData(58, 50)]
    [InlineData(107, 0)]
    [InlineData(-20, 100)]
    [InlineData(140, 0)]
    public void Resolve_MapsAndClampsVerticalTrack(double pointerY, double expected)
    {
        double value = VerticalRangeMapper.Resolve(pointerY, 116, 9, 0, 100);

        Assert.Equal(expected, value, precision: 6);
    }
}
