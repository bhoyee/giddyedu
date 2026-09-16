using GiddyEdu.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GiddyEdu.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GiddyEduDbContext>
{
    public GiddyEduDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Set ConnectionStrings__Postgres before running Entity Framework design-time commands.");
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public")).Options;
        return new GiddyEduDbContext(options, new TenantContextAccessor());
    }
}
