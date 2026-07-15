/**
 * Configuration for the RestrictPoint SDK client.
 *
 * The SDK hides all licensing complexity: consumers supply identifiers
 * and the SDK handles bootstrap, validation, caching, and telemetry
 * automatically (docs/14-SDK-Architecture.md).
 */
export interface RestrictPointClientOptions {
  /** RestrictPoint API base URL, e.g. https://api.restrictpoint.com */
  apiBaseUrl: string;

  /** Project identifier assigned in the developer portal. */
  projectId: string;

  /** Customer tenant identifier. */
  tenantId: string;

  /** Stable installation identifier for this deployment. */
  installationId: string;
}
