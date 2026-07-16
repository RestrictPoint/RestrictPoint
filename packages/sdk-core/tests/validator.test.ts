import { describe, expect, it } from "vitest";
import { LicensingApiClient } from "../src/api.js";
import { LayeredCache } from "../src/cache.js";
import { KeySetResolver } from "../src/keyset.js";
import { validateOffline } from "../src/validator.js";
import { createTestSigner, testContext, testPayload, MemoryStorage, type TestSigner } from "./helpers.js";

function resolverFor(signer: TestSigner, clock?: () => number): KeySetResolver {
  const fetchImpl: typeof fetch = async () =>
    new Response(JSON.stringify({ keys: [signer.publicJwk] }), { status: 200 });

  const api = new LicensingApiClient("https://licensing.test", fetchImpl);
  return new KeySetResolver(api, new LayeredCache(new MemoryStorage(), clock));
}

function unreachableResolver(): KeySetResolver {
  const fetchImpl: typeof fetch = async () => {
    throw new TypeError("network unreachable");
  };
  const api = new LicensingApiClient("https://licensing.test", fetchImpl);
  return new KeySetResolver(api, new LayeredCache(new MemoryStorage()));
}

const options = { gracePeriodDays: 7, clock: () => Date.now() };

describe("validateOffline", () => {
  it("accepts a validly signed, bound, unexpired license", async () => {
    const signer = await createTestSigner();
    const token = await signer.sign(testPayload());

    const result = await validateOffline(token, testContext(), resolverFor(signer), options);

    expect(result.isValid).toBe(true);
    expect(result.status).toBe("active");
    expect(result.features["Export"]).toBe(true);
    expect(result.limits["maxUsers"]).toBe(25);
    expect(result.source).toBe("offline");
  });

  it("rejects a tampered payload (signature mismatch)", async () => {
    const signer = await createTestSigner();
    const token = await signer.sign(testPayload());
    const parts = token.split(".");
    const tampered = testPayload({ features: { Export: true, Advanced: true } });
    const forgedPayload = Buffer.from(JSON.stringify(tampered))
      .toString("base64")
      .replace(/\+/g, "-")
      .replace(/\//g, "_")
      .replace(/=+$/, "");

    const result = await validateOffline(
      `${parts[0]}.${forgedPayload}.${parts[2]}`,
      testContext(),
      resolverFor(signer),
      options,
    );

    expect(result.isValid).toBe(false);
    expect(result.failureReason).toBe("invalid_signature");
    expect(result.features).toEqual({}); // Nothing leaks from an unverified payload.
  });

  it("rejects a token signed by a different key", async () => {
    const signer = await createTestSigner("key-a");
    const attacker = await createTestSigner("key-a"); // Same kid, different key material.
    const token = await attacker.sign(testPayload());

    const result = await validateOffline(token, testContext(), resolverFor(signer), options);

    expect(result.failureReason).toBe("invalid_signature");
  });

  it("rejects an unknown signing key after key set refresh", async () => {
    const signer = await createTestSigner("known-key");
    const token = await signer.sign(testPayload(), "rotated-away-key");

    const result = await validateOffline(token, testContext(), resolverFor(signer), options);

    expect(result.failureReason).toBe("unknown_signing_key");
  });

  it("fails closed when the key set is unreachable and uncached", async () => {
    const signer = await createTestSigner();
    const token = await signer.sign(testPayload());

    const result = await validateOffline(token, testContext(), unreachableResolver(), options);

    expect(result.isValid).toBe(false);
    expect(result.failureReason).toBe("keys_unavailable");
  });

  it.each([
    ["tenantId", "99999999-9999-4999-8999-999999999999", "tenant_mismatch"],
    ["projectId", "99999999-9999-4999-8999-999999999999", "project_mismatch"],
    ["webPartGuid", "99999999-9999-4999-8999-999999999999", "webpart_mismatch"],
  ] as const)("rejects binding mismatch on %s", async (field, value, reason) => {
    const signer = await createTestSigner();
    const token = await signer.sign(testPayload());

    const result = await validateOffline(
      token,
      { ...testContext(), [field]: value },
      resolverFor(signer),
      options,
    );

    expect(result.isValid).toBe(false);
    expect(result.failureReason).toBe(reason);
  });

  it("binding comparison is case-insensitive (GUID casing)", async () => {
    const signer = await createTestSigner();
    const token = await signer.sign(testPayload());

    const context = testContext();
    const result = await validateOffline(
      token,
      { ...context, tenantId: context.tenantId.toUpperCase() },
      resolverFor(signer),
      options,
    );

    expect(result.isValid).toBe(true);
  });

  it("enters grace after expiry, within the grace window", async () => {
    const signer = await createTestSigner();
    const expired = testPayload({ expiresAt: Math.floor(Date.now() / 1000) - 24 * 3600 }); // 1 day ago
    const token = await signer.sign(expired);

    const result = await validateOffline(token, testContext(), resolverFor(signer), options);

    expect(result.isValid).toBe(true);
    expect(result.status).toBe("grace"); // Features stay enabled during grace (docs/10).
    expect(result.features["Export"]).toBe(true);
  });

  it("expires fully after the grace window", async () => {
    const signer = await createTestSigner();
    const longGone = testPayload({ expiresAt: Math.floor(Date.now() / 1000) - 10 * 24 * 3600 }); // 10 days ago
    const token = await signer.sign(longGone);

    const result = await validateOffline(token, testContext(), resolverFor(signer), options);

    expect(result.isValid).toBe(false);
    expect(result.status).toBe("expired");
  });

  it("treats a null expiry as a lifetime license", async () => {
    const signer = await createTestSigner();
    const token = await signer.sign(testPayload({ expiresAt: null }));

    const result = await validateOffline(token, testContext(), resolverFor(signer), options);

    expect(result.isValid).toBe(true);
    expect(result.status).toBe("active");
    expect(result.expiresAt).toBeNull();
  });

  it("returns a structured failure for garbage input, never throws", async () => {
    const signer = await createTestSigner();

    const result = await validateOffline("garbage", testContext(), resolverFor(signer), options);

    expect(result.isValid).toBe(false);
    expect(result.failureReason).toBe("malformed_token");
  });
});
