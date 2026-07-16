import { LicensingApiClient } from "./api.js";
import { CacheKeys, CacheTtl, LayeredCache } from "./cache.js";
import { generateNonce } from "./crypto.js";
import { KeySetResolver } from "./keyset.js";
import type { RestrictPointClientOptions } from "./options.js";
import { TelemetryQueue } from "./telemetry.js";
import { validateOffline, type LicenseContext, type LicenseResult } from "./validator.js";

/** Listener invoked whenever the license state changes. */
export type LicenseListener = (result: LicenseResult) => void;

const DEFAULT_GRACE_DAYS = 7;
const SDK_VERSION = "0.1.0";

/**
 * The RestrictPoint SDK client (docs/14 boot sequence).
 *
 * Offline-first: a cached or cryptographically verified local result renders the app
 * immediately; the licensing API is only consulted to refresh state and pick up
 * revocations. The client never throws into the host application — every failure is a
 * structured {@link LicenseResult}.
 */
export class RestrictPointClient {
  private readonly options: RestrictPointClientOptions;
  private readonly cache: LayeredCache;
  private readonly api: LicensingApiClient;
  private readonly keys: KeySetResolver;
  private readonly telemetry: TelemetryQueue;
  private readonly listeners = new Set<LicenseListener>();
  private readonly clock: () => number;

  private current: LicenseResult | null = null;
  private refreshing: Promise<void> | null = null;

  public constructor(options: RestrictPointClientOptions) {
    this.options = options;
    this.clock = options.clock ?? (() => Date.now());
    this.cache = new LayeredCache(options.storage, this.clock);
    this.api = new LicensingApiClient(options.apiBaseUrl, options.fetchImpl);
    this.keys = new KeySetResolver(this.api, this.cache);
    this.telemetry = new TelemetryQueue(options.telemetryTransport ?? null, this.clock);
  }

  /** The most recent license result, or null before initialization. */
  public get license(): LicenseResult | null {
    return this.current;
  }

  /**
   * Boots the SDK (docs/14): cached result → offline cryptographic validation →
   * background online refresh. Resolves as soon as a render decision is available.
   */
  public async initialize(): Promise<LicenseResult> {
    // 1. Cached result renders instantly (<10ms target).
    const cached = this.cache.get<LicenseResult>(CacheKeys.license(this.options.projectId));
    if (cached) {
      const fromCache: LicenseResult = { ...cached, source: "cache" };
      this.setResult(fromCache);
      void this.refreshOnline(); // Revocation pickup happens in the background.
      return this.current ?? fromCache;
    }

    // 2. Offline cryptographic validation.
    const offline = await validateOffline(this.options.licenseToken, this.context(), this.keys, {
      gracePeriodDays: this.options.gracePeriodDays ?? DEFAULT_GRACE_DAYS,
      clock: this.clock,
    });

    if (offline.isValid) {
      this.telemetry.emit("licenseValidated", { source: "offline", status: offline.status });
      this.setResult(offline);
      this.cacheResult(offline);
      void this.refreshOnline();
      return this.current ?? offline;
    }

    // 3. Offline validation failed (no key material, revocation unknown, or genuinely
    //    invalid): the online path is authoritative.
    const online = await this.refreshOnlineCore();
    if (online && this.current) {
      return this.current;
    }

    // 4. Network unavailable and no cache: the offline outcome stands.
    this.telemetry.emit(
      offline.failureReason === "keys_unavailable" ? "offlineModeActivated" : "licenseRejected",
      { reason: offline.failureReason ?? "unknown" },
    );
    this.setResult(offline);
    return offline;
  }

  /** Forces an online validation refresh. */
  public async refresh(): Promise<LicenseResult> {
    await this.refreshOnlineCore();
    return this.current ?? blockedResult("not_initialized");
  }

  /** Evaluates a feature flag (<1ms — pure map lookup). */
  public isFeatureEnabled(feature: string): boolean {
    const enabled = this.current?.isValid === true && this.current.features[feature] === true;
    this.telemetry.emit("featureAccessed", { feature, enabled: String(enabled) });
    return enabled;
  }

  /** Entitlement limits from the license (empty when unlicensed). */
  public get limits(): Record<string, number> {
    return this.current?.limits ?? {};
  }

  /** Subscribes to license state changes. Returns an unsubscribe function. */
  public subscribe(listener: LicenseListener): () => void {
    this.listeners.add(listener);
    if (this.current) {
      listener(this.current);
    }
    return () => this.listeners.delete(listener);
  }

  /** Flushes buffered telemetry. */
  public flushTelemetry(): Promise<void> {
    return this.telemetry.flush();
  }

  private context(): LicenseContext {
    return {
      tenantId: this.options.tenantId,
      projectId: this.options.projectId,
      webPartGuid: this.options.webPartGuid,
      installationId: this.options.installationId,
    };
  }

  private async refreshOnline(): Promise<void> {
    this.refreshing ??= this.refreshOnlineCore()
      .then(() => undefined)
      .finally(() => {
        this.refreshing = null;
      });
    await this.refreshing;
  }

  /** Returns true when the online call produced an authoritative result. */
  private async refreshOnlineCore(): Promise<boolean> {
    try {
      const response = await this.api.validate({
        licenseToken: this.options.licenseToken,
        tenantId: this.options.tenantId,
        projectId: this.options.projectId,
        webPartGuid: this.options.webPartGuid,
        installationId: this.options.installationId,
        nonce: generateNonce(),
        timestampUtc: new Date(this.clock()).toISOString(),
        sdkVersion: this.options.sdkVersion ?? SDK_VERSION,
      });

      const result: LicenseResult = {
        isValid: response.isValid,
        status: normalizeStatus(response.status, response.isValid),
        features: response.features,
        limits: response.limits,
        expiresAt: response.expiresAt ?? null,
        source: "online",
        ...(response.failureReason !== undefined && { failureReason: response.failureReason }),
      };

      this.telemetry.emit(response.isValid ? "licenseValidated" : "licenseRejected", {
        source: "online",
        status: result.status,
      });

      this.setResult(result);
      this.cacheResult(result);
      return true;
    } catch {
      // Network/API failure: offline mode — cached/offline state remains in force.
      this.telemetry.emit("fallbackTriggered", { reason: "api_unreachable" });
      return false;
    }
  }

  private cacheResult(result: LicenseResult): void {
    // Never cache a blocked state: recovery must not require cache expiry.
    if (result.isValid) {
      this.cache.set(CacheKeys.license(this.options.projectId), result, CacheTtl.licenseMs);
    } else {
      this.cache.remove(CacheKeys.license(this.options.projectId));
    }
  }

  private setResult(result: LicenseResult): void {
    this.current = result;
    for (const listener of this.listeners) {
      try {
        listener(result);
      } catch {
        // Listener errors never propagate into the SDK.
      }
    }
  }
}

function normalizeStatus(status: string, isValid: boolean): LicenseResult["status"] {
  switch (status) {
    case "active":
    case "expired":
    case "grace":
    case "revoked":
      return status;
    default:
      return isValid ? "active" : "invalid";
  }
}

function blockedResult(reason: string): LicenseResult {
  return {
    isValid: false,
    status: "invalid",
    features: {},
    limits: {},
    expiresAt: null,
    failureReason: reason,
    source: "offline",
  };
}
