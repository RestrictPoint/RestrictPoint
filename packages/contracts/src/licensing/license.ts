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

/** Request body for POST /v1/licenses/validate (docs/16-API-Specification.md). */
export interface LicenseValidationRequest {
  licenseToken: string;
  tenantId: string;
  projectId: string;
  webPartGuid: string;
  installationId: string;
}

/** Response body for POST /v1/licenses/validate. */
export interface LicenseValidationResponse {
  isValid: boolean;
  status: LicenseStatus;
  features: Record<string, boolean>;
  limits: Record<string, number>;
  expiresAt: string | null;
}
