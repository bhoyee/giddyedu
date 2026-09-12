using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.StudentLifecycle;
using GiddyEdu.Infrastructure.Tenancy;
using GiddyEdu.Modules.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GiddyEdu.SecurityTests;

public sealed class PhaseOneEndpointSecurityContractTests
{
    [Fact]
    public async Task TenantMiddleware_RejectsMissingMembership_AndEstablishesOnlyAuthorisedTenant()
    {
        var tenantContext = new TenantContextAccessor(); var tenantId = Guid.NewGuid(); var userId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<GiddyEduDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new GiddyEduDbContext(options, tenantContext);
        db.Tenants.Add(new Tenant(tenantId, "Security school", $"security-{tenantId:N}", DateTimeOffset.UtcNow)); await db.SaveChangesAsync();
        var nextCalled = false; var middleware = new TenantResolutionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var denied = AuthenticatedContext(userId, tenantId); await middleware.InvokeAsync(denied, tenantContext, db);
        Assert.Equal(StatusCodes.Status403Forbidden, denied.Response.StatusCode); Assert.False(nextCalled); Assert.False(tenantContext.HasTenant);

        tenantContext.Set(tenantId, null); db.TenantMemberships.Add(new TenantMembership(Guid.NewGuid(), tenantId, userId, DateTimeOffset.UtcNow)); await db.SaveChangesAsync(); tenantContext.Clear(); db.ChangeTracker.Clear();
        var allowed = AuthenticatedContext(userId, tenantId); await middleware.InvokeAsync(allowed, tenantContext, db);
        Assert.True(nextCalled); Assert.False(tenantContext.HasTenant);
    }
    [Fact]
    public void PublicPhaseOneEndpoints_AreExplicitlyAnonymousAndRateLimited()
    {
        var endpoints = BuildEndpoints();
        var anonymous = endpoints.Where(x => x.Metadata.GetMetadata<IAllowAnonymous>() is not null).ToArray();

        Assert.NotEmpty(anonymous);
        Assert.All(anonymous, endpoint => Assert.True(
            endpoint.RoutePattern.RawText == "/api/v1/plans" ||
            endpoint.RoutePattern.RawText == "/api/v1/account-invitations/accept" ||
            endpoint.RoutePattern.RawText!.StartsWith("/api/v1/public/admissions/", StringComparison.Ordinal)));
        Assert.All(anonymous.Where(x => x.RoutePattern.RawText != "/api/v1/plans"), endpoint => Assert.Equal("auth", endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName));
    }

    [Fact]
    public void PlatformAdministration_RequiresGlobalRole_AndMutationRateLimit()
    {
        var endpoints = BuildEndpoints().Where(x => x.RoutePattern.RawText!.StartsWith("/api/v1/platform/admin/", StringComparison.Ordinal)).ToArray();

        Assert.Equal(7, endpoints.Length);
        Assert.All(endpoints, endpoint => Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()));
        Assert.All(endpoints.Where(x => !HttpMethods.IsGet(x.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single())), endpoint => Assert.Equal("administration", endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName));
    }

    [Fact]
    public void ImportsExportsAndDocuments_HaveDedicatedDataTransferRateLimit()
    {
        var transferEndpoints = BuildEndpoints().Where(x =>
            x.RoutePattern.RawText!.StartsWith("/api/v1/documents", StringComparison.Ordinal) ||
            x.RoutePattern.RawText.EndsWith("export.csv", StringComparison.Ordinal) ||
            x.RoutePattern.RawText.EndsWith("/sensitive", StringComparison.Ordinal) ||
            x.RoutePattern.RawText.Contains("/next-of-kin", StringComparison.Ordinal) ||
            x.RoutePattern.RawText.EndsWith("/errors", StringComparison.Ordinal) ||
            x.RoutePattern.RawText.EndsWith("imports/uploads", StringComparison.Ordinal) ||
            x.RoutePattern.RawText.Contains("imports/{operationId:guid}/complete", StringComparison.Ordinal)).ToArray();

        Assert.NotEmpty(transferEndpoints);
        Assert.All(transferEndpoints, endpoint => Assert.Equal("data-transfer", endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName));
        Assert.All(transferEndpoints, endpoint => Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>()));
    }

    private static RouteEndpoint[] BuildEndpoints()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddRouting();
        foreach (var contract in new[] { typeof(IDataPortabilityService).Assembly, typeof(ITenantContext).Assembly }
                     .SelectMany(x => x.GetTypes()).Where(x => x.IsInterface && !x.IsGenericType))
            serviceCollection.AddSingleton(contract, _ => throw new InvalidOperationException("Endpoint metadata tests do not resolve application services."));
        serviceCollection.AddSingleton<GiddyEduDbContext>(_ => throw new InvalidOperationException("Endpoint metadata tests do not resolve the database."));
        var services = serviceCollection.BuildServiceProvider();
        var routeBuilder = new TestEndpointRouteBuilder(services);
        routeBuilder.MapPhaseOneEndpoints();
        return routeBuilder.DataSources.SelectMany(x => x.Endpoints).Cast<RouteEndpoint>().ToArray();
    }

    private static DefaultHttpContext AuthenticatedContext(Guid userId, Guid tenantId)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim("tenant_id", tenantId.ToString())], "test"));
        return context;
    }

    private sealed class TestEndpointRouteBuilder(IServiceProvider serviceProvider) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;
        public ICollection<EndpointDataSource> DataSources { get; } = [];
        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(ServiceProvider);
    }
}
