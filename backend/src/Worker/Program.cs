using GiddyEdu.Infrastructure;
using GiddyEdu.Worker;
using GiddyEdu.Infrastructure.Messaging;
using Hangfire;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddGiddyEduFoundation(builder.Configuration, "GiddyEdu.Worker");
builder.Services.AddHangfireServer(options => options.WorkerCount = Math.Max(1, Environment.ProcessorCount / 2));
builder.Services.AddTransient<FoundationHeartbeatJob>();

var host = builder.Build();
host.Services.GetRequiredService<IBackgroundJobClient>().Enqueue<FoundationHeartbeatJob>(job => job.ExecuteAsync());
host.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<NotificationOutboxSweepJob>("notification-outbox-sweep", job => job.ExecuteAsync(CancellationToken.None), Cron.Minutely);
host.Run();
