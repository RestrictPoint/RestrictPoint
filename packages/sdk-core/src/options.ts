import type { FetchLike } from "./api.js";
import type { Clock, KeyValueStorage } from "./cache.js";
import type { TelemetryTransport } from "./telemetry.js";

/**
 * Configuration for the RestrictPoint SDK client.
 *
 * The SDK hides all licensing complexity: consumers supply identifiers and the signed
 * license token; the SDK handles bootstrap, offline validation, caching, refresh, and
 * telemetry automatically (docs/14-SDK-Architecture.md).
 */
export interface RestrictPointClientOptions {
  /** Licensing API base URL, e.g. https://api.restrictpoint.com */
  apiBaseUrl: string;

  /** Project identifier assigned in the developer portal. */
  projectId: string;

  /** Customer SharePoint tenant identifier. */
  tenantId: string;

  /** Web part manifest GUID this instance enforces. */
  webPartGuid: string;

  /** Stable installation identifier for this deployment. */
  installationId: string;

  /** The signed JWS license token issued to the customer. */
  licenseToken: string;

  /** Grace period after expiry in days (docs/10: 7–30). Default 7. */
  gracePeriodDays?: number;

  /** SDK version reported to the licensing service. */
  sdkVersion?: string;

  /** Injectable fetch (tests, non-browser hosts). */
  fetchImpl?: FetchLike;

  /** Injectable persistent storage. Defaults to localStorage when available. */
  storage?: KeyValueStorage | null;

  /** Injectable clock in epoch milliseconds (tests). */
  clock?: Clock;

  /** Optional telemetry sink. When omitted, events are buffered but not sent. */
  telemetryTransport?: TelemetryTransport;
}
