using RestrictPoint.Api.Licensing.Domain;

namespace RestrictPoint.Api.Licensing.Application.Common;

/// <summary>Builds signed license payloads from domain state (docs/10 license model).</summary>
public static class LicensePayloadFactory
{
    public static LicensePayload Create(License license, string tokenId, Guid? installationId = null)
    {
        ArgumentNullException.ThrowIfNull(license);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);

        return new LicensePayload
        {
            TokenId = tokenId,
            LicenseId = license.Id,
            ProjectId = license.ProjectId,
            TenantId = license.CustomerTenantId,
            CustomerId = license.CustomerOrganizationId,
            LicenseType = license.LicenseType.ToString(),
            IssuedAt = license.IssuedUtc.ToUnixTimeSeconds(),
            ExpiresAt = license.ExpiresUtc?.ToUnixTimeSeconds(),
            Features = license.Features
                .Where(f => !f.IsDeleted)
                .ToDictionary(f => f.FeatureKey, f => f.Enabled),
            Limits = license.Limits
                .Where(l => !l.IsDeleted)
                .ToDictionary(l => l.LimitKey, l => l.Value),
            InstallationId = installationId,
            WebPartGuids = license.WebParts
                .Where(w => !w.IsDeleted)
                .Select(w => w.WebPartGuid)
                .ToList(),
            Version = license.Version,
        };
    }
}
