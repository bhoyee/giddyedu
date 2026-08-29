using GiddyEdu.BuildingBlocks.Time;

namespace GiddyEdu.UnitTests;

public sealed class ClockTests
{
    [Fact]
    public void SystemClock_ReturnsUtcTimestamp()
    {
        var clock = new SystemClock();

        Assert.Equal(TimeSpan.Zero, clock.UtcNow.Offset);
    }
}
