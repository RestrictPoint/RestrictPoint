# RestrictPoint

Enterprise Licensing Platform for SPFx and Beyond.

This is the deployable source monorepo. Architecture documentation lives in the repository-level `docs/` folder; the compact system map lives in [ARCHITECTURE_INDEX.md](./ARCHITECTURE_INDEX.md).

## Structure

```
apps/            Deployable applications (React frontends + Azure Function APIs)
packages/        Shared packages (contracts, SDKs, UI)
infrastructure/  Terraform Infrastructure as Code
tools/           Repository tooling
samples/         SDK integration samples
tests/           Cross-cutting integration and e2e tests
```

Organized per docs/05-Solution-Structure.md (Modular Monorepo, business-capability first).

## Prerequisites

- Node.js 22 LTS + pnpm 9
- .NET 10 SDK
- Azure Functions Core Tools v4
- Terraform >= 1.9

## Getting Started

```bash
pnpm install
pnpm build          # builds all TypeScript workspaces via TurboRepo
dotnet build        # builds all .NET Function Apps
```

## Rules

- Applications never reference other applications.
- Only the owning bounded context writes to its database schema.
- All cross-context communication is event-driven (see Event Catalog).
- Managed Identity everywhere; no secrets in source.
- Regenerate ARCHITECTURE_INDEX.md whenever the architecture changes.
