namespace GiddyEdu.ArchitectureTests;

public sealed class ArchitectureSmokeTests
{
    [Fact]
    public void RegisteredModules_AreAvailable()
    {
        Assert.NotNull(typeof(GiddyEdu.Modules.Platform.ModuleMarker));
        Assert.NotNull(typeof(GiddyEdu.Modules.Tenancy.ModuleMarker));
        Assert.NotNull(typeof(GiddyEdu.Modules.Identity.ModuleMarker));
        Assert.NotNull(typeof(GiddyEdu.Modules.Subscriptions.ModuleMarker));
        Assert.NotNull(typeof(GiddyEdu.Modules.Schools.ModuleMarker));
        Assert.NotNull(typeof(GiddyEdu.Modules.Academics.ModuleMarker));
        Assert.NotNull(typeof(GiddyEdu.Modules.Hr.ModuleMarker));
        Assert.NotNull(typeof(GiddyEdu.Modules.StudentLifecycle.ModuleMarker));
    }

    [Fact]
    public void DomainModules_DoNotReferenceInfrastructure()
    {
        var modules = new[] { typeof(GiddyEdu.Modules.Platform.ModuleMarker).Assembly, typeof(GiddyEdu.Modules.Tenancy.ModuleMarker).Assembly, typeof(GiddyEdu.Modules.Identity.ModuleMarker).Assembly, typeof(GiddyEdu.Modules.Subscriptions.ModuleMarker).Assembly, typeof(GiddyEdu.Modules.Schools.ModuleMarker).Assembly, typeof(GiddyEdu.Modules.Academics.ModuleMarker).Assembly, typeof(GiddyEdu.Modules.Hr.ModuleMarker).Assembly, typeof(GiddyEdu.Modules.StudentLifecycle.ModuleMarker).Assembly };
        Assert.All(modules, module => Assert.DoesNotContain(module.GetReferencedAssemblies(), reference => reference.Name == "GiddyEdu.Infrastructure"));
    }
}
