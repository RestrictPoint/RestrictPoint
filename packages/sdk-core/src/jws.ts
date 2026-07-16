import type { LicenseTokenPayload } from "@restrictpoint/contracts";
import { base64UrlToBytes, base64UrlToString } from "./base64url.js";

/** Decoded JWS compact license token, ready for signature verification. */
export interface DecodedLicenseToken {
  /** JWS header. Only ES256 is ever accepted. */
  header: { alg: string; typ?: string; kid?: string };

  /** The signed license claims. */
  payload: LicenseTokenPayload;

  /** ASCII bytes of `header.payload` — the signature input. */
  signingInput: Uint8Array;

  /** IEEE P-1363 (r||s, 64 bytes) ES256 signature. */
  signature: Uint8Array;
}

/** Structured decode failure. The SDK never throws into the host app. */
export class LicenseTokenError extends Error {
  public constructor(
    public readonly code: string,
    message: string,
  ) {
    super(message);
    this.name = "LicenseTokenError";
  }
}

/**
 * Decodes a JWS compact license token without verifying it. Rejects anything that is not
 * structurally a three-part ES256 token — algorithm substitution is refused at decode
 * time, mirroring the server-side verifier (docs/10).
 */
export function decodeLicenseToken(token: string): DecodedLicenseToken {
  if (typeof token !== "string" || token.length === 0) {
    throw new LicenseTokenError("malformed_token", "License token is empty.");
  }

  const parts = token.split(".");
  if (parts.length !== 3 || parts.some((p) => p.length === 0)) {
    throw new LicenseTokenError("malformed_token", "License token is not JWS compact form.");
  }

  const [headerPart, payloadPart, signaturePart] = parts as [string, string, string];

  let header: DecodedLicenseToken["header"];
  let payload: LicenseTokenPayload | null;
  let signature: Uint8Array;
  try {
    header = JSON.parse(base64UrlToString(headerPart)) as DecodedLicenseToken["header"];
    payload = JSON.parse(base64UrlToString(payloadPart)) as LicenseTokenPayload | null;
    signature = base64UrlToBytes(signaturePart);
  } catch {
    throw new LicenseTokenError("malformed_token", "License token segments are not valid base64url JSON.");
  }

  if (header.alg !== "ES256") {
    // Reject anything but ES256 — prevents algorithm-substitution attacks.
    throw new LicenseTokenError("malformed_token", "License token algorithm must be ES256.");
  }

  if (signature.length !== 64) {
    throw new LicenseTokenError("malformed_token", "ES256 signature must be 64 bytes (P-1363).");
  }

  if (!payload || typeof payload.licenseId !== "string" || !Array.isArray(payload.webPartGuids)) {
    throw new LicenseTokenError("malformed_token", "License payload is missing required claims.");
  }

  return {
    header,
    payload,
    signingInput: new TextEncoder().encode(`${headerPart}.${payloadPart}`),
    signature,
  };
}
