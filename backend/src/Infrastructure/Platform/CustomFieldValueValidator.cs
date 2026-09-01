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
            if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return !definition.IsRequired;
            if (definition.IsRequired && value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())) return false;
            if (!MatchesType(definition.DataType, value)) return false;
            return MatchesConfiguredRules(definition.ValidationJson, value);
        }
        catch (JsonException) { return false; }
    }

    private static bool MatchesType(CustomFieldDataType type, JsonElement value) => type switch
    {
        CustomFieldDataType.ShortText or CustomFieldDataType.LongText => value.ValueKind == JsonValueKind.String,
        CustomFieldDataType.Phone => value.ValueKind == JsonValueKind.String && IsPhone(value.GetString()),
        CustomFieldDataType.Integer => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        CustomFieldDataType.Decimal => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
        CustomFieldDataType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        CustomFieldDataType.Date => value.ValueKind == JsonValueKind.String && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        CustomFieldDataType.DateTime => value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _),
        CustomFieldDataType.Email => value.ValueKind == JsonValueKind.String && IsEmail(value.GetString()),
        CustomFieldDataType.Url => value.ValueKind == JsonValueKind.String && Uri.TryCreate(value.GetString(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https",
        CustomFieldDataType.SingleSelect => value.ValueKind == JsonValueKind.String,
        CustomFieldDataType.MultiSelect => value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(x => x.ValueKind == JsonValueKind.String),
        _ => false
    };

    private static bool MatchesConfiguredRules(string? rulesJson, JsonElement value)
    {
        if (string.IsNullOrWhiteSpace(rulesJson)) return true;
        using var rulesDocument = JsonDocument.Parse(rulesJson);
        if (rulesDocument.RootElement.ValueKind != JsonValueKind.Object) return false;
        var rules = rulesDocument.RootElement;
        if (value.ValueKind == JsonValueKind.String)
        {
            var length = value.GetString()!.Length;
            if (rules.TryGetProperty("minLength", out var min) && (!min.TryGetInt32(out var minValue) || length < minValue)) return false;
            if (rules.TryGetProperty("maxLength", out var max) && (!max.TryGetInt32(out var maxValue) || length > maxValue)) return false;
        }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            if (rules.TryGetProperty("minimum", out var min) && (!min.TryGetDecimal(out var minValue) || number < minValue)) return false;
            if (rules.TryGetProperty("maximum", out var max) && (!max.TryGetDecimal(out var maxValue) || number > maxValue)) return false;
        }
        return true;
    }

    private static bool IsEmail(string? value)
    {
        try { return value is not null && new MailAddress(value).Address == value; }
        catch (FormatException) { return false; }
    }

    private static bool IsPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var digits = value.Count(char.IsDigit);
        return digits is >= 7 and <= 15 && value.All(c => char.IsDigit(c) || c is '+' or ' ' or '-' or '(' or ')');
    }
}
