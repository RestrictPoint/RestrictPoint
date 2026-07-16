import { describe, expect, it } from "vitest";
import { CacheKeys, CacheTtl, LayeredCache } from "../src/cache.js";
import { TelemetryQueue, type TelemetryEvent } from "../src/telemetry.js";
import { MemoryStorage } from "./helpers.js";

describe("LayeredCache", () => {
  it("returns stored values before TTL expiry", () => {
    let now = 1_000;
    const cache = new LayeredCache(new MemoryStorage(), () => now);

    cache.set("k", { a: 1 }, 5_000);
    now = 5_999;

    expect(cache.get<{ a: number }>("k")).toEqual({ a: 1 });
  });

  it("expires values after TTL", () => {
    let now = 1_000;
    const cache = new LayeredCache(new MemoryStorage(), () => now);

    cache.set("k", "v", 5_000);
    now = 6_001;

    expect(cache.get("k")).toBeNull();
  });

  it("survives storage round-trips across instances (persistence layer)", () => {
    const storage = new MemoryStorage();
    new LayeredCache(storage, () => 0).set("k", 42, 10_000);

    const second = new LayeredCache(storage, () => 5_000);
    expect(second.get<number>("k")).toBe(42);
  });

  it("operates memory-only when storage is unavailable", () => {
    const cache = new LayeredCache(null, () => 0);

    cache.set("k", "v", 1_000);

    expect(cache.get("k")).toBe("v");
  });

  it("treats corrupt storage entries as misses", () => {
    const storage = new MemoryStorage();
    storage.setItem("k", "{not json");

    expect(new LayeredCache(storage).get("k")).toBeNull();
  });

  it("removes entries from both layers", () => {
    const storage = new MemoryStorage();
    const cache = new LayeredCache(storage, () => 0);
    cache.set("k", "v", 10_000);

    cache.remove("k");

    expect(cache.get("k")).toBeNull();
    expect(storage.getItem("k")).toBeNull();
  });

  it("uses the documented key conventions and TTLs", () => {
    expect(CacheKeys.license("p1")).toBe("rp_license_p1");
    expect(CacheTtl.licenseMs).toBe(12 * 60 * 60 * 1000);
    expect(CacheTtl.keySetMs).toBe(24 * 60 * 60 * 1000);
  });
});

describe("TelemetryQueue", () => {
  it("buffers events without a transport", () => {
    const queue = new TelemetryQueue(null);

    queue.emit("licenseValidated", { source: "offline" });

    expect(queue.pending).toHaveLength(1);
    expect(queue.pending[0]?.name).toBe("licenseValidated");
  });

  it("bounds the buffer, dropping oldest events", () => {
    const queue = new TelemetryQueue(null);

    for (let i = 0; i < 150; i += 1) {
      queue.emit("featureAccessed", { i: String(i) });
    }

    expect(queue.pending).toHaveLength(100);
    expect(queue.pending[0]?.properties["i"]).toBe("50");
  });

  it("flushes batches to the transport and clears the buffer", async () => {
    const delivered: TelemetryEvent[][] = [];
    const queue = new TelemetryQueue((events) => {
      delivered.push([...events]);
    });

    queue.emit("licenseValidated");
    queue.emit("licenseRejected");
    await queue.flush();

    expect(delivered).toHaveLength(1);
    expect(delivered[0]).toHaveLength(2);
    expect(queue.pending).toHaveLength(0);
  });

  it("swallows transport failures", async () => {
    const queue = new TelemetryQueue(() => {
      throw new Error("boom");
    });

    queue.emit("fallbackTriggered");

    await expect(queue.flush()).resolves.toBeUndefined();
  });
});
