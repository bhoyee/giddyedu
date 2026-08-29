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
}
