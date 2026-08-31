using GiddyEdu.Infrastructure.Platform;
using GiddyEdu.Infrastructure.Storage;
using GiddyEdu.Modules.Platform.Domain;

namespace GiddyEdu.UnitTests;

public sealed class FoundationSecurityTests
{
    [Fact]
    public void ObjectKeys_AreTenantScopedAndServerGenerated()
    {
        var tenantId = Guid.NewGuid();
        var key = new TenantObjectKeyFactory().Create(tenantId, "students", "documents", Guid.NewGuid(), Guid.NewGuid(), "pdf");
        Assert.StartsWith($"tenants/{tenantId:D}/students/documents/", key, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => new TenantObjectKeyFactory().Create(tenantId, "../escape", "documents", Guid.NewGuid(), Guid.NewGuid(), "pdf"));
    }

    [Theory]
    [InlineData(CustomFieldDataType.Integer, "123", true)]
    [InlineData(CustomFieldDataType.Integer, "\"123\"", false)]
    [InlineData(CustomFieldDataType.Email, "\"parent@example.com\"", true)]
    [InlineData(CustomFieldDataType.Email, "\"invalid\"", false)]
    [InlineData(CustomFieldDataType.MultiSelect, "[\"a\",\"b\"]", true)]
    public void CustomFieldValues_AreValidatedByDeclaredType(CustomFieldDataType type, string json, bool expected)
    {
        var definition = new CustomFieldDefinition(Guid.NewGuid(), Guid.NewGuid(), "Platform", "Tenant", "field", "Field", type, DateTimeOffset.UtcNow);
        Assert.Equal(expected, new CustomFieldValueValidator().IsValid(definition, json));
    }
}
