import { describe, expect, it } from "vitest";
import type { LicenseValidationResponse } from "@restrictpoint/contracts";
import { RestrictPointClient } from "../src/client.js";
import type { RestrictPointClientOptions } from "../src/options.js";
import { createTestSigner, testContext, testPayload, MemoryStorage, type TestSigner } from "./helpers.js";

interface Recorded {
  url: string;
  body?: unknown;
}

function envelope(data: unknown): Response {
  return new Response(
    JSON.stringify({ data, correlationId: "corr", timestamp: new Date().toISOString() }),
    { status: 200 },
  );
}

function buildFetch(
  signer: TestSigner,
  validation: LicenseValidationResponse | "offline" | ((calls: number) => LicenseValidationResponse),
  recorded: Recorded[] = [],
): typeof fetch {
  let validateCalls = 0;

  return async (input, init) => {
    const url = String(input);
    const entry: Recorded = { url };
    if (init?.body) {
      entry.body = JSON.parse(String(init.body));
    }
    recorded.push(entry);

    if (url.endsWith("/v1/licenses/keys")) {
      return new Response(JSON.stringify({ keys: [signer.publicJwk] }), { status: 200 });
    }

    if (url.endsWith("/v1/licenses/validate")) {
      if (validation === "offline") {
        throw new TypeError("network unreachable");
      }
      validateCalls += 1;
      const body = typeof validation === "function" ? validation(validateCalls) : validation;
      return envelope(body);
    }

    return new Response(null, { status: 404 });
  };
}

async function buildClient(
  signer: TestSigner,
  overrides: Partial<RestrictPointClientOptions>,
): Promise<RestrictPointClient> {
  const context = testContext();
  return new RestrictPointClient({
    apiBaseUrl: "https://licensing.test",
    projectId: context.projectId,
    tenantId: context.tenantId,
    webPartGuid: context.webPartGuid,
    installationId: context.installationId,
    licenseToken: await signer.sign(testPayload()),
    storage: new MemoryStorage(),
    ...overrides,
  });
}

const activeResponse: LicenseValidationResponse = {
  isValid: true,
  status: "active",
  features: { Export: true },
  limits: { maxUsers: 25 },
  expiresAt: new Date(Date.now() + 30 * 24 * 3600 * 1000).toISOString(),
};

describe("RestrictPointClient", () => {
  it("boots offline and refreshes online in the background", async () => {
    const signer = await createTestSigner();
    const recorded: Recorded[] = [];
    const client = await buildClient(signer, {
      fetchImpl: buildFetch(signer, activeResponse, recorded),
    });

    const result = await client.initialize();

    expect(result.isValid).toBe(true);
    expect(["offline", "online"]).toContain(result.source);
    expect(client.isFeatureEnabled("Export")).toBe(true);
    expect(client.isFeatureEnabled("Nonexistent")).toBe(false);
    expect(client.limits["maxUsers"]).toBe(25);
  });

  it("renders from a valid license fully offline (API unreachable, keys cached)", async () => {
    const signer = await createTestSigner();
    const storage = new MemoryStorage();

    // First boot: network available — caches license + key set.
    const first = await buildClient(signer, {
      storage,
      fetchImpl: buildFetch(signer, activeResponse),
    });
    await first.initialize();

    // Second boot: network gone.
    const second = await buildClient(signer, {
      storage,
      fetchImpl: buildFetch(signer, "offline"),
    });
    const result = await second.initialize();

    expect(result.isValid).toBe(true);
    expect(result.source).toBe("cache");
  });

  it("sends replay-protection fields on online validation", async () => {
    const signer = await createTestSigner();
    const recorded: Recorded[] = [];
    const client = await buildClient(signer, {
      fetchImpl: buildFetch(signer, activeResponse, recorded),
    });

    await client.initialize();
    await client.refresh();

    const validate = recorded.find((r) => r.url.endsWith("/validate"));
    const body = validate?.body as Record<string, unknown>;
    expect(body["nonce"]).toBeTruthy();
    expect(body["timestampUtc"]).toBeTruthy();
    expect(body["installationId"]).toBe(testContext().installationId);
  });

  it("revocation via online refresh overrides the offline result and clears the cache", async () => {
    const signer = await createTestSigner();
    const storage = new MemoryStorage();
    const revoked: LicenseValidationResponse = {
      isValid: false,
      status: "revoked",
      features: {},
      limits: {},
      expiresAt: null,
      failureReason: "revoked",
    };

    const client = await buildClient(signer, {
      storage,
      fetchImpl: buildFetch(signer, revoked),
    });

    await client.initialize();
    const result = await client.refresh();

    expect(result.isValid).toBe(false);
    expect(result.status).toBe("revoked");
    expect(client.isFeatureEnabled("Export")).toBe(false);

    // A subsequent boot must not resurrect the revoked license from cache.
    const rebooted = await buildClient(signer, {
      storage,
      fetchImpl: buildFetch(signer, "offline"),
    });
    const rebootResult = await rebooted.initialize();
    expect(rebootResult.source).not.toBe("cache");
  });

  it("falls back to the offline verdict when the API is unreachable and nothing is cached", async () => {
    const signer = await createTestSigner();

    // Keys endpoint works (needed for offline verification); validate endpoint down.
    const fetchImpl: typeof fetch = async (input) => {
      const url = String(input);
      if (url.endsWith("/keys")) {
        return new Response(JSON.stringify({ keys: [signer.publicJwk] }), { status: 200 });
      }
      throw new TypeError("network unreachable");
    };

    const client = await buildClient(signer, { fetchImpl });
    const result = await client.initialize();

    expect(result.isValid).toBe(true); // Cryptographic verification carried the decision.
    expect(result.source).toBe("offline");
  });

  it("blocks when the token is invalid and the API confirms nothing", async () => {
    const signer = await createTestSigner();
    const client = await buildClient(signer, {
      licenseToken: "garbage-token",
      fetchImpl: buildFetch(signer, "offline"),
    });

    const result = await client.initialize();

    expect(result.isValid).toBe(false);
    expect(client.isFeatureEnabled("Export")).toBe(false);
  });

  it("notifies subscribers on state changes", async () => {
    const signer = await createTestSigner();
    const client = await buildClient(signer, {
      fetchImpl: buildFetch(signer, activeResponse),
    });

    const seen: string[] = [];
    client.subscribe((result) => seen.push(result.source));

    await client.initialize();
    await client.refresh();

    expect(seen.length).toBeGreaterThanOrEqual(1);
    expect(seen).toContain("online");
  });
});
