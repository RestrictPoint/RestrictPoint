import { CacheKeys, CacheTtl, LayeredCache } from "@restrictpoint/sdk-core";

/**
 * SPFx-specific license context captured from the SharePoint host
 * (docs/02-Domain-Mode.md, Installation aggregate).
 */
export interface SpfxLicenseContext {
  /** SharePoint tenant identifier. */
  tenantId: string;

  /** Site collection identifier hosting the web part. */
  siteCollectionId: string;

  /** Web part manifest GUID. */
  webPartGuid: string;

  /** SDK version embedded in the solution. */
  sdkVersion: string;

  /** Application version of the hosting solution. */
  applicationVersion: string;
}

/**
 * Structural subset of the SPFx `WebPartContext` the SDK reads. Typed structurally so
 * the package carries no dependency on @microsoft/sp-* (docs/14: core stays host-agnostic).
 */
export interface SpfxHostContext {
  pageContext?: {
    aadInfo?: { tenantId?: string };
    site?: { id?: { toString(): string } };
  };
}

/**
 * Extracts the tenant and site identity from a live SPFx web part context.
 * Returns null when the host context does not carry AAD information.
 */
export function resolveSpfxContext(
  context: SpfxHostContext,
): { tenantId: string; siteCollectionId: string } | null {
  const tenantId = context.pageContext?.aadInfo?.tenantId;
  if (!tenantId) {
    return null;
  }

  return {
    tenantId,
    siteCollectionId: context.pageContext?.site?.id?.toString() ?? "",
  };
}

/**
 * Returns the stable installation id for this deployment, creating and persisting one on
 * first use (docs/10: first validation from a new installation id is activation).
 */
export function resolveInstallationId(tenantId: string, webPartGuid: string): string {
  const cache = new LayeredCache();
  const scope = `${tenantId}:${webPartGuid}`;
  const key = CacheKeys.installation(scope);

  const existing = cache.get<string>(key);
  if (existing) {
    // Sliding persistence: refresh the TTL on every boot.
    cache.set(key, existing, CacheTtl.installationMs);
    return existing;
  }

  const installationId = crypto.randomUUID();
  cache.set(key, installationId, CacheTtl.installationMs);
  return installationId;
}
