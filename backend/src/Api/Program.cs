using GiddyEdu.BuildingBlocks.Time;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddSingleton<IClock, SystemClock>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "GiddyEdu API",
    status = "running"
}));

app.MapGet("/api/v1/platform/info", (IClock clock) => Results.Ok(new
{
    name = "GiddyEdu",
    architecture = "modular-monolith",
    apiVersion = "v1",
    utcNow = clock.UtcNow
}));

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live");

app.Run();

public partial class Program;
