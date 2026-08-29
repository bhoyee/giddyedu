namespace GiddyEdu.BuildingBlocks.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
