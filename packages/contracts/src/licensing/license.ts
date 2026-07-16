/** License lifecycle status. */
export type LicenseStatus =
  | "active"
  | "expired"
  | "revoked"
  | "suspended"
  | "trial"
  | "grace";

/** Shared license representation exposed to clients and the SDK. */
export interface LicenseDto {
  licenseId: string;
  projectId: string;
  customerId: string;
  licenseType: string;
  status: LicenseStatus;
  issuedUtc: string;
  expiresUtc: string | null;
  features: Record<string, boolean>;
}

/** Request body for POST /v1/licenses/validate (docs/16 + docs/10 replay protection). */
export interface LicenseValidationRequest {
  licenseToken: string;
  tenantId: string;
  projectId: string;
  webPartGuid: string;
  installationId: string;
  /** Client-generated unique nonce (replay protection, docs/10). */
  nonce: string;
  /** Client clock at request time (ISO 8601); must be within ±5 minutes. */
  timestampUtc: string;
  sdkVersion?: string;
}

/** Response body for POST /v1/licenses/validate. */
export interface LicenseValidationResponse {
  isValid: boolean;
  status: LicenseStatus;
  features: Record<string, boolean>;
  limits: Record<string, number>;
  expiresAt: string | null;
  /** Machine-readable failure reason when isValid is false. */
  failureReason?: string;
}

/** Signed JWS license payload (docs/10 License Model, camelCase claims). */
export interface LicenseTokenPayload {
  /** Token id (JWS jti). */
  jti: string;
  licenseId: string;
  projectId: string;
  /** The customer SharePoint tenant the license is bound to. */
  tenantId: string;
  /** The customer organization id. */
  customerId: string;
  licenseType: string;
  /** Unix epoch seconds. */
  issuedAt: number;
  /** Unix epoch seconds. Null/absent for lifetime licenses. */
  expiresAt?: number | null;
  features: Record<string, boolean>;
  limits: Record<string, number>;
  installationId?: string | null;
  webPartGuids: string[];
  version: number;
}

/** A single ES256 public key from GET /v1/licenses/keys (RFC 7517). */
export interface LicenseJwk {
  kty: "EC";
  crv: "P-256";
  use: "sig";
  alg: "ES256";
  kid: string;
  x: string;
  y: string;
}

/** JWKS document from GET /v1/licenses/keys. */
export interface LicenseJwks {
  keys: LicenseJwk[];
}
