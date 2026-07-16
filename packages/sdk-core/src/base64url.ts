/**
 * Base64url helpers (RFC 4648 §5) — no padding, URL-safe alphabet.
 * Pure functions usable in browsers and Node ≥18.
 */

/** Decodes a base64url string to bytes. */
export function base64UrlToBytes(value: string): Uint8Array {
  const padded = value.replace(/-/g, "+").replace(/_/g, "/");
  const withPadding = padded + "=".repeat((4 - (padded.length % 4)) % 4);
  const binary = atob(withPadding);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i += 1) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes;
}

/** Decodes a base64url string to a UTF-8 string. */
export function base64UrlToString(value: string): string {
  return new TextDecoder().decode(base64UrlToBytes(value));
}

/** Encodes bytes as base64url. */
export function bytesToBase64Url(bytes: Uint8Array): string {
  let binary = "";
  for (const byte of bytes) {
    binary += String.fromCharCode(byte);
  }
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}
