using System.Text.Json;
using System.Text.Json.Serialization;
using RestrictPoint.Api.Billing.Domain;
using RestrictPoint.Common;

namespace RestrictPoint.Api.Billing.Application.Common;

/// <summary>
/// The license template captured at checkout and applied by the Licensing service on
/// SubscriptionActivated. Stored serialized on the subscription; embedded in the event
/// payload so the event is self-contained (docs/20 payload design rules).
/// </summary>
public sealed record LicenseTemplate
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly string[] ValidLicenseTypes =
        ["Trial", "Monthly", "Annual", "Enterprise", "Lifetime"];

    public required string LicenseType { get; init; }

    public required IReadOnlyDictionary<string, bool> Features { get; init; }

    public required IReadOnlyDictionary<string, int> Limits { get; init; }

    public required IReadOnlyList<Guid> WebPartGuids { get; init; }

    public string Serialize() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Parses and validates a template. Invalid templates are rejected at checkout.</summary>
    public static Result<LicenseTemplate> Parse(string json)
    {
        LicenseTemplate? template;
        try
        {
            template = JsonSerializer.Deserialize<LicenseTemplate>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return BillingErrors.InvalidLicenseTemplate;
        }

        return template is not null && template.IsValid()
            ? template
            : BillingErrors.InvalidLicenseTemplate;
    }

    public bool IsValid() =>
        ValidLicenseTypes.Contains(LicenseType, StringComparer.Ordinal)
        && WebPartGuids.Count > 0
        && WebPartGuids.All(g => g != Guid.Empty);
}
