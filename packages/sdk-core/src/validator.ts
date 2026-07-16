import type { LicenseTokenPayload } from "@restrictpoint/contracts";
import type { KeySetResolver } from "./keyset.js";
import type { Clock } from "./cache.js";
import { decodeLicenseToken, LicenseTokenError } from "./jws.js";
import { verifySignature } from "./crypto.js";

/** The effective license state after evaluation (docs/14 LicenseResult). */
export type LicenseState = "active" | "expired" | "grace" | "revoked" | "invalid";

/** Result of validating a license (docs/14 LicenseResult shape). */
export interface LicenseResult {
  isValid: boolean;
  status: LicenseState;
  features: Record<string, boolean>;
  limits: Record<string, number>;
  /** ISO 8601, null for lifetime licenses. */
  expiresAt: string | null;
  /** Machine-readable reason when the license is not usable. */
  failureReason?: string;
  /** How the result was produced. */
  source: "offline" | "online" | "cache";
}

/** Binding identity the license must match (docs/14 LicenseContext). */
export interface LicenseContext {
  tenantId: string;
  projectId: string;
  webPartGuid: string;
  installationId: string;
}

/** Offline validation configuration. */
export interface OfflineValidationOptions {
  /** Grace period after expiry during which features remain enabled (docs/10: 7–30 days). */
  gracePeriodDays: number;
  clock: Clock;
}

const BLOCKED: Omit<LicenseResult, "failureReason"> = {
  isValid: false,
  status: "invalid",
  features: {},
  limits: {},
  expiresAt: null,
  source: "offline",
};

/**
 * Offline-first license validation (docs/14 boot sequence, steps mirroring the server
 * pipeline): decode → verify ES256 signature → tenant/project/webPart binding →
 * expiry + grace evaluation. Only cryptographic verification is trusted; this function
 * never throws — every failure is a structured blocked result.
 */
export async function validateOffline(
  token: string,
  context: LicenseContext,
  keys: KeySetResolver,
  options: OfflineValidationOptions,
): Promise<LicenseResult> {
  // 1-2. Decode and structural checks (ES256 enforced at decode time).
  let decoded;
  try {
    decoded = decodeLicenseToken(token);
  } catch (error) {
    const reason = error instanceof LicenseTokenError ? error.code : "malformed_token";
    return { ...BLOCKED, failureReason: reason };
  }

  // 3. Signature verification via WebCrypto.
  const kid = decoded.header.kid;
  if (!kid) {
    return { ...BLOCKED, failureReason: "unknown_signing_key" };
  }

  let key: CryptoKey | null;
  try {
    key = await keys.getKey(kid);
  } catch {
    // Key set unavailable and not cached: cannot verify — fail closed offline;
    // the caller falls back to online validation.
    return { ...BLOCKED, failureReason: "keys_unavailable" };
  }

  if (!key) {
    return { ...BLOCKED, failureReason: "unknown_signing_key" };
  }

  const signatureValid = await verifySignature(key, decoded.signature, decoded.signingInput);
  if (!signatureValid) {
    return { ...BLOCKED, failureReason: "invalid_signature" };
  }

  // 4. Binding: tenant + project + webPart GUID must all match (docs/10).
  const payload = decoded.payload;
  if (!equalsIgnoreCase(payload.tenantId, context.tenantId)) {
    return { ...BLOCKED, failureReason: "tenant_mismatch" };
  }

  if (!equalsIgnoreCase(payload.projectId, context.projectId)) {
    return { ...BLOCKED, failureReason: "project_mismatch" };
  }

  if (!payload.webPartGuids.some((g) => equalsIgnoreCase(g, context.webPartGuid))) {
    return { ...BLOCKED, failureReason: "webpart_mismatch" };
  }

  // 5. Expiry and grace evaluation.
  return evaluateExpiry(payload, options);
}

function evaluateExpiry(payload: LicenseTokenPayload, options: OfflineValidationOptions): LicenseResult {
  const expiresAtIso =
    payload.expiresAt === null || payload.expiresAt === undefined
      ? null
      : new Date(payload.expiresAt * 1000).toISOString();

  const base = {
    features: payload.features,
    limits: payload.limits,
    expiresAt: expiresAtIso,
    source: "offline" as const,
  };

  if (payload.expiresAt === null || payload.expiresAt === undefined) {
    return { ...base, isValid: true, status: "active" };
  }

  const nowMs = options.clock();
  const expiresMs = payload.expiresAt * 1000;
  const graceEndsMs = expiresMs + options.gracePeriodDays * 24 * 60 * 60 * 1000;

  if (nowMs < expiresMs) {
    return { ...base, isValid: true, status: "active" };
  }

  if (nowMs < graceEndsMs) {
    // Grace: features remain enabled, UI shows a warning (docs/10 grace system).
    return { ...base, isValid: true, status: "grace", failureReason: "expired" };
  }

  return { ...base, isValid: false, status: "expired", failureReason: "expired" };
}

function equalsIgnoreCase(a: string, b: string): boolean {
  return a.localeCompare(b, undefined, { sensitivity: "accent" }) === 0;
}
