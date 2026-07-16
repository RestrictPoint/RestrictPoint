/**
 * Layered cache (docs/14): memory first, then a persistent Web Storage layer. Every
 * entry carries an absolute expiry; reads past the TTL are misses. The cache degrades
 * gracefully when storage is unavailable (private browsing, quota, SSR).
 */

/** Minimal storage abstraction — matches Web Storage, injectable for tests. */
export interface KeyValueStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

interface CacheEnvelope<T> {
  value: T;
  expiresAtMs: number;
}

/** Injectable clock for deterministic tests. */
export type Clock = () => number;

export class LayeredCache {
  private readonly memory = new Map<string, CacheEnvelope<unknown>>();
  private readonly storage: KeyValueStorage | null;
  private readonly now: Clock;

  public constructor(storage?: KeyValueStorage | null, clock?: Clock) {
    this.storage = storage ?? defaultStorage();
    this.now = clock ?? (() => Date.now());
  }

  // eslint-disable-next-line @typescript-eslint/no-unnecessary-type-parameters -- typed read API; callers declare the stored shape
  public get<T>(key: string): T | null {
    const fromMemory = this.memory.get(key);
    if (fromMemory) {
      if (fromMemory.expiresAtMs > this.now()) {
        return fromMemory.value as T;
      }
      this.memory.delete(key);
    }

    if (!this.storage) {
      return null;
    }

    try {
      const raw = this.storage.getItem(key);
      if (raw === null) {
        return null;
      }

      const envelope = JSON.parse(raw) as CacheEnvelope<T>;
      if (envelope.expiresAtMs <= this.now()) {
        this.storage.removeItem(key);
        return null;
      }

      this.memory.set(key, envelope);
      return envelope.value;
    } catch {
      return null; // Corrupt or inaccessible storage is a cache miss, never an error.
    }
  }

  // eslint-disable-next-line @typescript-eslint/no-unnecessary-type-parameters -- symmetrical with get<T>
  public set<T>(key: string, value: T, ttlMs: number): void {
    const envelope: CacheEnvelope<T> = { value, expiresAtMs: this.now() + ttlMs };
    this.memory.set(key, envelope);

    try {
      this.storage?.setItem(key, JSON.stringify(envelope));
    } catch {
      // Storage quota/unavailability never breaks the SDK.
    }
  }

  public remove(key: string): void {
    this.memory.delete(key);
    try {
      this.storage?.removeItem(key);
    } catch {
      // Ignore.
    }
  }
}

/** Cache key conventions (docs/14). */
export const CacheKeys = {
  license: (projectId: string): string => `rp_license_${projectId}`,
  keySet: (): string => "rp_jwks",
  installation: (installationId: string): string => `rp_install_${installationId}`,
} as const;

/** Cache TTLs (docs/10 + docs/14). */
export const CacheTtl = {
  licenseMs: 12 * 60 * 60 * 1000, // 12h
  keySetMs: 24 * 60 * 60 * 1000, // 24h
  installationMs: 24 * 60 * 60 * 1000, // 24h
} as const;

function defaultStorage(): KeyValueStorage | null {
  try {
    // localStorage may throw on access in restricted contexts.
    const storage = globalThis.localStorage;
    const probe = "__rp_probe__";
    storage.setItem(probe, "1");
    storage.removeItem(probe);
    return storage;
  } catch {
    return null;
  }
}
