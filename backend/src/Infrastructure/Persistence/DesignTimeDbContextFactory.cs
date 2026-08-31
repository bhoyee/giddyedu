using GiddyEdu.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GiddyEdu.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GiddyEduDbContext>
{
    public GiddyEduDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=giddyedu;Username=giddyedu;Password=giddyedu_dev";
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseNpgsql(connection).Options;
        return new GiddyEduDbContext(options, new TenantContextAccessor());
    }
}
