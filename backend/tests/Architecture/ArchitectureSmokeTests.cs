namespace GiddyEdu.ArchitectureTests;

public sealed class ArchitectureSmokeTests
{
    [Fact]
    public void PhaseZero_Modules_AreAvailable()
    {
        Assert.NotNull(typeof(GiddyEdu.Modules.Platform.ModuleMarker));
        Assert.NotNull(typeof(GiddyEdu.Modules.Tenancy.ModuleMarker));
        Assert.NotNull(typeof(GiddyEdu.Modules.Identity.ModuleMarker));
        Assert.NotNull(typeof(GiddyEdu.Modules.Subscriptions.ModuleMarker));
    }

    [Fact]
    public void DomainModules_DoNotReferenceInfrastructure()
    {
        var modules = new[] { typeof(GiddyEdu.Modules.Platform.ModuleMarker).Assembly, typeof(GiddyEdu.Modules.Tenancy.ModuleMarker).Assembly, typeof(GiddyEdu.Modules.Identity.ModuleMarker).Assembly, typeof(GiddyEdu.Modules.Subscriptions.ModuleMarker).Assembly };
        Assert.All(modules, module => Assert.DoesNotContain(module.GetReferencedAssemblies(), reference => reference.Name == "GiddyEdu.Infrastructure"));
    }
}
