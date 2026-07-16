import type { LicenseJwk, LicenseTokenPayload } from "@restrictpoint/contracts";
import { bytesToBase64Url } from "../src/base64url.js";

/** A test ES256 signer: real WebCrypto keys, real signatures. */
export interface TestSigner {
  kid: string;
  publicJwk: LicenseJwk;
  sign(payload: LicenseTokenPayload, kidOverride?: string, algOverride?: string): Promise<string>;
}

export async function createTestSigner(kid = "test-key-1"): Promise<TestSigner> {
  const keyPair = await crypto.subtle.generateKey({ name: "ECDSA", namedCurve: "P-256" }, true, [
    "sign",
    "verify",
  ]);

  const exported = (await crypto.subtle.exportKey("jwk", keyPair.publicKey)) as JsonWebKey;

  const publicJwk: LicenseJwk = {
    kty: "EC",
    crv: "P-256",
    use: "sig",
    alg: "ES256",
    kid,
    x: exported.x!,
    y: exported.y!,
  };

  return {
    kid,
    publicJwk,
    async sign(payload, kidOverride, algOverride) {
      const header = { alg: algOverride ?? "ES256", typ: "JWT", kid: kidOverride ?? kid };
      const encoder = new TextEncoder();
      const headerPart = bytesToBase64Url(encoder.encode(JSON.stringify(header)));
      const payloadPart = bytesToBase64Url(encoder.encode(JSON.stringify(payload)));
      const signingInput = `${headerPart}.${payloadPart}`;

      const signature = await crypto.subtle.sign(
        { name: "ECDSA", hash: "SHA-256" },
        keyPair.privateKey,
        encoder.encode(signingInput),
      );

      return `${signingInput}.${bytesToBase64Url(new Uint8Array(signature))}`;
    },
  };
}

/** A valid default payload; override fields per test. */
export function testPayload(overrides: Partial<LicenseTokenPayload> = {}): LicenseTokenPayload {
  return {
    jti: "token-1",
    licenseId: "0e0f0a0b-0000-4000-8000-000000000001",
    projectId: "11111111-1111-4111-8111-111111111111",
    tenantId: "22222222-2222-4222-8222-222222222222",
    customerId: "33333333-3333-4333-8333-333333333333",
    licenseType: "Annual",
    issuedAt: Math.floor(Date.now() / 1000) - 3600,
    expiresAt: Math.floor(Date.now() / 1000) + 30 * 24 * 3600,
    features: { Export: true, Advanced: false },
    limits: { maxUsers: 25 },
    webPartGuids: ["44444444-4444-4444-8444-444444444444"],
    version: 1,
    ...overrides,
  };
}

/** The binding context matching {@link testPayload}. */
export function testContext() {
  return {
    tenantId: "22222222-2222-4222-8222-222222222222",
    projectId: "11111111-1111-4111-8111-111111111111",
    webPartGuid: "44444444-4444-4444-8444-444444444444",
    installationId: "55555555-5555-4555-8555-555555555555",
  };
}

/** In-memory Web Storage for cache tests. */
export class MemoryStorage {
  private readonly map = new Map<string, string>();

  getItem(key: string): string | null {
    return this.map.get(key) ?? null;
  }

  setItem(key: string, value: string): void {
    this.map.set(key, value);
  }

  removeItem(key: string): void {
    this.map.delete(key);
  }
}
