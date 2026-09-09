namespace GiddyEdu.Modules.Tenancy.Domain;

public sealed class Tenant
{
    private Tenant() { }

    public Tenant(Guid id, string name, string slug, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("A tenant identifier is required.", nameof(id));
        Id = id;
        Name = Require(name, nameof(name), 200);
        Slug = Require(slug, nameof(slug), 100).ToLowerInvariant();
        CreatedAtUtc = createdAtUtc;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public void Suspend(DateTimeOffset now) { IsActive = false; UpdatedAtUtc = now; }
    public void Reactivate(DateTimeOffset now) { IsActive = true; UpdatedAtUtc = now; }

    private static string Require(string value, string name, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength)
            throw new ArgumentException($"{name} is required and must not exceed {maxLength} characters.", name);
        return normalized;
    }
}
