# Samples

SDK integration samples (SPFx web parts, React apps, Teams apps).

## SPFx web part (React)

The SDK hides all licensing complexity — wrap your component tree and gate features:

```tsx
import * as React from "react";
import { RestrictPoint, useFeature, useEntitlements } from "@restrictpoint/sdk-spfx";

// Inside your SPFx web part's render():
export function LicensedApp(props: { context: WebPartContext; licenseToken: string }) {
  return (
    <RestrictPoint
      apiBaseUrl="https://rp-dev-func-licensing.azurewebsites.net/api"
      projectId="11111111-1111-4111-8111-111111111111"
      webPartGuid="44444444-4444-4444-8444-444444444444"
      licenseToken={props.licenseToken}
      spfxContext={props.context}
    >
      <Dashboard />
    </RestrictPoint>
  );
}

function Dashboard() {
  const canExport = useFeature("Export");
  const limits = useEntitlements();

  return (
    <div>
      <h1>Analytics Dashboard</h1>
      {canExport && <button>Export to Excel</button>}
      <p>Licensed for up to {limits.maxUsers ?? 0} users.</p>
    </div>
  );
}
```

What happens automatically (docs/14):

- Tenant + installation identity resolved from the SPFx context
- License verified **offline** via WebCrypto ES256 against the public JWKS
  (`GET /v1/licenses/keys`) — no API round-trip on the render path
- Result cached 12h (memory + localStorage); background online refresh picks up
  revocations
- Expired licenses get a grace-period banner; revoked/invalid licenses render a
  blocked notice (both overridable via `graceBanner` / `blockedFallback` props)
- Replay-protected online validation (`nonce` + `timestampUtc`) when the API is used

## Non-React / custom hosts

Use `@restrictpoint/sdk-core` directly:

```ts
import { RestrictPointClient } from "@restrictpoint/sdk-core";

const client = new RestrictPointClient({
  apiBaseUrl: "https://rp-dev-func-licensing.azurewebsites.net/api",
  projectId: "...",
  tenantId: "...",
  webPartGuid: "...",
  installationId: "...",
  licenseToken: "...",
});

const license = await client.initialize();
if (license.isValid && client.isFeatureEnabled("Export")) {
  // render the licensed experience
}
```
