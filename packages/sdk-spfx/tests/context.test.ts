import { describe, expect, it } from "vitest";
import { resolveSpfxContext } from "../src/context.js";

describe("resolveSpfxContext", () => {
  it("extracts tenant and site collection identity from a live SPFx context", () => {
    const resolved = resolveSpfxContext({
      pageContext: {
        aadInfo: { tenantId: "22222222-2222-4222-8222-222222222222" },
        site: { id: { toString: () => "site-collection-1" } },
      },
    });

    expect(resolved).toEqual({
      tenantId: "22222222-2222-4222-8222-222222222222",
      siteCollectionId: "site-collection-1",
    });
  });

  it("returns null when the host carries no AAD information", () => {
    expect(resolveSpfxContext({})).toBeNull();
    expect(resolveSpfxContext({ pageContext: {} })).toBeNull();
  });

  it("tolerates a missing site id", () => {
    const resolved = resolveSpfxContext({
      pageContext: { aadInfo: { tenantId: "t1" } },
    });

    expect(resolved).toEqual({ tenantId: "t1", siteCollectionId: "" });
  });
});
