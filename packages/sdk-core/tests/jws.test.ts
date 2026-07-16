import { describe, expect, it } from "vitest";
import { base64UrlToBytes, base64UrlToString, bytesToBase64Url } from "../src/base64url.js";
import { decodeLicenseToken, LicenseTokenError } from "../src/jws.js";
import { createTestSigner, testPayload } from "./helpers.js";

describe("base64url", () => {
  it("round-trips bytes", () => {
    const bytes = new Uint8Array([0, 1, 2, 250, 251, 252, 253, 254, 255]);
    expect(base64UrlToBytes(bytesToBase64Url(bytes))).toEqual(bytes);
  });

  it("produces URL-safe output without padding", () => {
    const encoded = bytesToBase64Url(new Uint8Array([251, 255, 254, 62, 63]));
    expect(encoded).not.toMatch(/[+/=]/);
  });

  it("decodes UTF-8 strings", () => {
    const encoded = bytesToBase64Url(new TextEncoder().encode('{"alg":"ES256"}'));
    expect(base64UrlToString(encoded)).toBe('{"alg":"ES256"}');
  });
});

describe("decodeLicenseToken", () => {
  it("decodes a well-formed ES256 token", async () => {
    const signer = await createTestSigner();
    const token = await signer.sign(testPayload());

    const decoded = decodeLicenseToken(token);

    expect(decoded.header.alg).toBe("ES256");
    expect(decoded.header.kid).toBe(signer.kid);
    expect(decoded.payload.licenseId).toBe(testPayload().licenseId);
    expect(decoded.signature).toHaveLength(64);
  });

  it.each(["", "a.b", "a.b.c.d", "not-a-token"])("rejects structurally invalid token %#", (token) => {
    expect(() => decodeLicenseToken(token)).toThrowError(LicenseTokenError);
  });

  it("rejects algorithm substitution", async () => {
    const signer = await createTestSigner();
    const token = await signer.sign(testPayload(), undefined, "none");

    expect(() => decodeLicenseToken(token)).toThrowError(/ES256/);
  });

  it("rejects tokens with truncated signatures", async () => {
    const signer = await createTestSigner();
    const token = await signer.sign(testPayload());
    const parts = token.split(".");
    const truncated = `${parts[0]}.${parts[1]}.${bytesToBase64Url(new Uint8Array(10))}`;

    expect(() => decodeLicenseToken(truncated)).toThrowError(/64 bytes/);
  });

  it("rejects payloads missing required claims", async () => {
    const signer = await createTestSigner();
    const token = await signer.sign({ jti: "x" } as never);

    expect(() => decodeLicenseToken(token)).toThrowError(/required claims/);
  });
});
