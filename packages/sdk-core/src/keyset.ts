import type { LicenseJwk, LicenseJwks } from "@restrictpoint/contracts";
import type { LicensingApiClient } from "./api.js";
import { CacheKeys, CacheTtl, type LayeredCache } from "./cache.js";
import { importVerificationKey } from "./crypto.js";

/**
 * Resolves ES256 verification keys by kid. Keys are cached for 24h (docs/10); an unknown
 * kid triggers one forced refresh (key rotation happened since the last fetch) before
 * the token is rejected.
 */
export class KeySetResolver {
  private readonly imported = new Map<string, CryptoKey>();

  public constructor(
    private readonly api: LicensingApiClient,
    private readonly cache: LayeredCache,
  ) {}

  /** Returns the verification key for the kid, or null when unknown after refresh. */
  public async getKey(kid: string): Promise<CryptoKey | null> {
    const alreadyImported = this.imported.get(kid);
    if (alreadyImported) {
      return alreadyImported;
    }

    let jwk = this.findInCache(kid);

    if (!jwk) {
      // Unknown kid: force a refresh once — the signing key may have rotated.
      const jwks = await this.api.getKeySet();
      this.cache.set(CacheKeys.keySet(), jwks, CacheTtl.keySetMs);
      jwk = jwks.keys.find((k) => k.kid === kid) ?? null;
    }

    if (!jwk) {
      return null;
    }

    const key = await importVerificationKey(jwk);
    this.imported.set(kid, key);
    return key;
  }

  private findInCache(kid: string): LicenseJwk | null {
    const cached = this.cache.get<LicenseJwks>(CacheKeys.keySet());
    return cached?.keys.find((k) => k.kid === kid) ?? null;
  }
}
