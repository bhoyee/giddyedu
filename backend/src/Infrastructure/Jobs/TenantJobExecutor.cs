using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.Jobs;

public sealed record TenantJobEnvelope(Guid TenantId, Guid? CampusId, string JobType, string PayloadJson);

public interface ITenantJobHandler
{
    string JobType { get; }
    Task HandleAsync(string payloadJson, CancellationToken cancellationToken);
}

public sealed class TenantJobExecutor(GiddyEduDbContext db, ITenantContextSetter tenantSetter, IEnumerable<ITenantJobHandler> handlers)
{
    public async Task ExecuteAsync(TenantJobEnvelope envelope, CancellationToken cancellationToken)
    {
        var activeTenant = await db.Tenants.AsNoTracking().AnyAsync(x => x.Id == envelope.TenantId && x.IsActive, cancellationToken);
        if (!activeTenant) throw new InvalidOperationException("The background-job tenant is not active.");
        tenantSetter.Set(envelope.TenantId, envelope.CampusId);
        try
        {
            var handler = handlers.SingleOrDefault(x => x.JobType == envelope.JobType) ?? throw new InvalidOperationException($"No handler is registered for job type '{envelope.JobType}'.");
            await handler.HandleAsync(envelope.PayloadJson, cancellationToken);
        }
        finally { tenantSetter.Clear(); }
    }
}
