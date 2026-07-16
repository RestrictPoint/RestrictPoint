import type {
  ApiErrorResponse,
  LicenseJwks,
  LicenseValidationRequest,
  LicenseValidationResponse,
} from "@restrictpoint/contracts";

/** Injectable fetch for tests and non-browser hosts. */
export type FetchLike = typeof fetch;

/** Structured API failure — the SDK converts these into fallback states, never throws. */
export class LicensingApiError extends Error {
  public constructor(
    public readonly code: string,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "LicensingApiError";
  }
}

/**
 * Minimal client for the Licensing service (docs/16 envelope). Only two endpoints are
 * ever called by the SDK: validation refresh and the public JWKS.
 */
export class LicensingApiClient {
  private readonly baseUrl: string;
  private readonly fetchImpl: FetchLike;

  public constructor(apiBaseUrl: string, fetchImpl?: FetchLike) {
    this.baseUrl = apiBaseUrl.replace(/\/+$/, "");
    this.fetchImpl = fetchImpl ?? fetch.bind(globalThis);
  }

  /** POST /v1/licenses/validate — online validation / refresh. */
  public async validate(
    request: LicenseValidationRequest,
    signal?: AbortSignal,
  ): Promise<LicenseValidationResponse> {
    const init: RequestInit = {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    };
    if (signal) {
      init.signal = signal;
    }
    const response = await this.fetchImpl(`${this.baseUrl}/v1/licenses/validate`, init);

    const body: unknown = await response.json().catch(() => null);

    if (!response.ok) {
      const error = (body as ApiErrorResponse | null)?.error;
      throw new LicensingApiError(
        error?.code ?? "licensing.unavailable",
        error?.message ?? `Licensing API returned ${String(response.status)}.`,
        response.status,
      );
    }

    const envelope = body as { data?: LicenseValidationResponse } | null;
    if (!envelope?.data || typeof envelope.data.isValid !== "boolean") {
      throw new LicensingApiError("licensing.malformed_response", "Unexpected validation response shape.");
    }

    return envelope.data;
  }

  /** GET /v1/licenses/keys — the ES256 public key set (raw JWKS, no envelope). */
  public async getKeySet(signal?: AbortSignal): Promise<LicenseJwks> {
    const init: RequestInit = {};
    if (signal) {
      init.signal = signal;
    }
    const response = await this.fetchImpl(`${this.baseUrl}/v1/licenses/keys`, init);

    if (!response.ok) {
      throw new LicensingApiError(
        "licensing.keys_unavailable",
        `JWKS endpoint returned ${String(response.status)}.`,
        response.status,
      );
    }

    const jwks = (await response.json().catch(() => null)) as LicenseJwks | null;
    if (!jwks || !Array.isArray(jwks.keys)) {
      throw new LicensingApiError("licensing.malformed_response", "Unexpected JWKS response shape.");
    }

    return jwks;
  }
}
