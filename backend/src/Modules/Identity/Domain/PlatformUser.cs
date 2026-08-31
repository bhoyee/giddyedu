using Microsoft.AspNetCore.Identity;

namespace GiddyEdu.Modules.Identity.Domain;

public sealed class PlatformUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
