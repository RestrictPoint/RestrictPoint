import {
  createContext,
  createElement,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactElement,
  type ReactNode,
} from "react";
import {
  RestrictPointClient,
  type LicenseResult,
  type RestrictPointClientOptions,
} from "@restrictpoint/sdk-core";
import { resolveInstallationId, resolveSpfxContext, type SpfxHostContext } from "./context.js";

/** Value exposed through the RestrictPoint React context. */
export interface RestrictPointContextValue {
  /** The SDK client (stable for the provider's lifetime). */
  client: RestrictPointClient;

  /** The current license result, or null while booting. */
  license: LicenseResult | null;

  /** True until the first render decision is available. */
  isLoading: boolean;
}

const RestrictPointReactContext = createContext<RestrictPointContextValue | null>(null);

/** Props for the {@link RestrictPoint} wrapper (docs/14 SPFx layer). */
export interface RestrictPointProps {
  /** Licensing API base URL. */
  apiBaseUrl: string;

  /** Project identifier assigned in the developer portal. */
  projectId: string;

  /** Web part manifest GUID. */
  webPartGuid: string;

  /** The signed license token issued to the customer. */
  licenseToken: string;

  /**
   * SPFx web part context. When provided, the tenant and installation identity are
   * resolved from the SharePoint host automatically.
   */
  spfxContext?: SpfxHostContext;

  /** Explicit tenant id (overrides SPFx resolution; required outside SharePoint). */
  tenantId?: string;

  /** Advanced client overrides (grace period, custom fetch/storage, telemetry). */
  clientOptions?: Partial<RestrictPointClientOptions>;

  /** Rendered while the license is being resolved. Defaults to nothing. */
  loadingFallback?: ReactNode;

  /** Rendered when the license is invalid/expired/revoked. Defaults to a minimal notice. */
  blockedFallback?: ReactNode;

  /** Rendered above children during the grace period. Defaults to a minimal banner. */
  graceBanner?: ReactNode;

  children: ReactNode;
}

/**
 * The SPFx wrapper (docs/14): resolves SharePoint identity, boots the SDK, and gates
 * rendering on the license state. Failure states follow the docs/14 matrix — blocked UI
 * when unlicensed/revoked, banner + full functionality during grace.
 */
export function RestrictPoint(props: RestrictPointProps): ReactElement | null {
  const tenantId =
    props.tenantId ?? (props.spfxContext ? (resolveSpfxContext(props.spfxContext)?.tenantId ?? "") : "");

  const client = useMemo(() => {
    return new RestrictPointClient({
      apiBaseUrl: props.apiBaseUrl,
      projectId: props.projectId,
      tenantId,
      webPartGuid: props.webPartGuid,
      installationId: resolveInstallationId(tenantId, props.webPartGuid),
      licenseToken: props.licenseToken,
      ...props.clientOptions,
    });
    // The wrapper identity is intentionally fixed for a mounted web part instance;
    // clientOptions are advanced overrides captured at mount time.
  }, [props.apiBaseUrl, props.projectId, tenantId, props.webPartGuid, props.licenseToken]);

  const [license, setLicense] = useState<LicenseResult | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let disposed = false;

    const unsubscribe = client.subscribe((result) => {
      if (!disposed) {
        setLicense(result);
      }
    });

    void client.initialize().finally(() => {
      if (!disposed) {
        setIsLoading(false);
      }
    });

    return () => {
      disposed = true;
      unsubscribe();
      void client.flushTelemetry();
    };
  }, [client]);

  const value = useMemo<RestrictPointContextValue>(
    () => ({ client, license, isLoading }),
    [client, license, isLoading],
  );

  let content: ReactNode;
  if (isLoading && license === null) {
    content = props.loadingFallback ?? null;
  } else if (license?.isValid && license.status === "grace") {
    content = createElement(
      "div",
      null,
      props.graceBanner ?? defaultGraceBanner(license),
      props.children,
    );
  } else if (license?.isValid) {
    content = props.children;
  } else {
    content = props.blockedFallback ?? defaultBlockedNotice(license);
  }

  return createElement(RestrictPointReactContext.Provider, { value }, content);
}

/** Access the raw SDK context. Throws when used outside <RestrictPoint>. */
export function useRestrictPoint(): RestrictPointContextValue {
  const value = useContext(RestrictPointReactContext);
  if (!value) {
    throw new Error("useRestrictPoint must be used within a <RestrictPoint> wrapper.");
  }
  return value;
}

/** The current license state plus a refresh method (docs/14 useLicense). */
export function useLicense(): {
  license: LicenseResult | null;
  isLoading: boolean;
  refresh: () => Promise<LicenseResult>;
} {
  const { client, license, isLoading } = useRestrictPoint();
  return { license, isLoading, refresh: () => client.refresh() };
}

/** Evaluates a feature flag (docs/14 useFeature). */
export function useFeature(feature: string): boolean {
  const { client, license } = useRestrictPoint();
  // license in the dependency chain re-evaluates on state changes.
  return useMemo(() => client.isFeatureEnabled(feature), [client, feature, license]);
}

/** Entitlement limits from the active license (docs/14 useEntitlements). */
export function useEntitlements(): Record<string, number> {
  const { license } = useRestrictPoint();
  return license?.limits ?? {};
}

function defaultGraceBanner(license: LicenseResult): ReactElement {
  return createElement(
    "div",
    { role: "alert", style: bannerStyle("#8a6d00", "#fff8e1") },
    `This product's license expired${license.expiresAt ? ` on ${new Date(license.expiresAt).toLocaleDateString()}` : ""}. ` +
      "It continues to work during the grace period — please renew.",
  );
}

function defaultBlockedNotice(license: LicenseResult | null): ReactElement {
  return createElement(
    "div",
    { role: "alert", style: bannerStyle("#8a1f11", "#fdecea") },
    license?.status === "revoked"
      ? "This product's license has been revoked."
      : "A valid license is required to use this product.",
  );
}

function bannerStyle(color: string, background: string): Record<string, string> {
  return {
    padding: "8px 12px",
    marginBottom: "8px",
    borderRadius: "4px",
    fontFamily: "'Segoe UI', sans-serif",
    fontSize: "13px",
    color,
    background,
  };
}
