namespace GiddyEdu.Worker;

public sealed class FoundationHeartbeatJob(ILogger<FoundationHeartbeatJob> logger)
{
    public Task ExecuteAsync()
    {
        logger.LogInformation("GiddyEdu background-job foundation heartbeat executed");
        return Task.CompletedTask;
    }
}
