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
