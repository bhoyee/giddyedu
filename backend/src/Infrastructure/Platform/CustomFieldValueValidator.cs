using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using GiddyEdu.Modules.Platform.Domain;

namespace GiddyEdu.Infrastructure.Platform;

public interface ICustomFieldValueValidator
{
    bool IsValid(CustomFieldDefinition definition, string valueJson);
}

public sealed class CustomFieldValueValidator : ICustomFieldValueValidator
{
    public bool IsValid(CustomFieldDefinition definition, string valueJson)
    {
        try
        {
            using var document = JsonDocument.Parse(valueJson);
            var value = document.RootElement;
            return definition.DataType switch
            {
                CustomFieldDataType.ShortText or CustomFieldDataType.LongText or CustomFieldDataType.Phone => value.ValueKind == JsonValueKind.String,
                CustomFieldDataType.Integer => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
                CustomFieldDataType.Decimal => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
                CustomFieldDataType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                CustomFieldDataType.Date => value.ValueKind == JsonValueKind.String && DateOnly.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                CustomFieldDataType.DateTime => value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _),
                CustomFieldDataType.Email => value.ValueKind == JsonValueKind.String && IsEmail(value.GetString()),
                CustomFieldDataType.Url => value.ValueKind == JsonValueKind.String && Uri.TryCreate(value.GetString(), UriKind.Absolute, out _),
                CustomFieldDataType.SingleSelect => value.ValueKind == JsonValueKind.String,
                CustomFieldDataType.MultiSelect => value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(x => x.ValueKind == JsonValueKind.String),
                _ => false
            };
        }
        catch (JsonException) { return false; }
    }

    private static bool IsEmail(string? value)
    {
        try { return value is not null && new MailAddress(value).Address == value; }
        catch (FormatException) { return false; }
    }
}
