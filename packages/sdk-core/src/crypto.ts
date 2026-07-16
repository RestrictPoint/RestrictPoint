import type { LicenseJwk } from "@restrictpoint/contracts";

/**
 * ES256 (ECDSA P-256 / SHA-256) verification via WebCrypto. Key Vault signatures are
 * IEEE P-1363 (r||s) — exactly what WebCrypto's ECDSA verify expects, so no format
 * conversion is required.
 */

/** Imports a P-256 public JWK for signature verification. */
export async function importVerificationKey(jwk: LicenseJwk): Promise<CryptoKey> {
  return crypto.subtle.importKey(
    "jwk",
    { kty: jwk.kty, crv: jwk.crv, x: jwk.x, y: jwk.y },
    { name: "ECDSA", namedCurve: "P-256" },
    false,
    ["verify"],
  );
}

/** Verifies an ES256 signature over the signing input. */
export async function verifySignature(
  key: CryptoKey,
  signature: Uint8Array,
  signingInput: Uint8Array,
): Promise<boolean> {
  return crypto.subtle.verify(
    { name: "ECDSA", hash: "SHA-256" },
    key,
    toArrayBuffer(signature),
    toArrayBuffer(signingInput),
  );
}

/** Generates a unique nonce for replay protection (docs/10). */
export function generateNonce(): string {
  return crypto.randomUUID();
}

function toArrayBuffer(bytes: Uint8Array): ArrayBuffer {
  return bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength) as ArrayBuffer;
}
