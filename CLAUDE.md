# CrewJet — Field Service Management

Modern field service management platform for trades businesses (starting with electrical contractors). Domain: `crewjet.io`. Tenants resolved by subdomain (`acme-electric.crewjet.io`); super-admin portal at `admin.crewjet.io`.

Solo developer (Kelly) on Windows + Rider + Docker Desktop.

---

## Current State vs. Target State

**This is important — the codebase is in early scaffolding. Do not assume target-state libraries are present.**

| Concern         | Current (on disk)                            | Target                                                    |
|-----------------|----------------------------------------------|-----------------------------------------------------------|
| Host project    | `FieldServiceManagement` (Blazor Server host)| Same                                                      |
| Client project  | `FieldServiceManagement.Client` (Blazor WASM)| Same                                                      |
| Persistence     | EF Core + SQLite (`ApplicationDbContext`)    | Marten (Postgres) primary; EF Core only for simple modules|
| Auth            | ASP.NET Core Identity (cookies)              | Microsoft 365 OIDC + SCIM provisioning                    |
| Messaging       | None                                         | Wolverine (commands, sagas, outbox)                       |
| UI styling      | Default Bootstrap                            | Tailwind CSS + custom dark component library              |
| Multi-tenancy   | None                                         | Subdomain resolution middleware + tenant-aware Marten     |

When asked to add a feature, **prefer extending what's already there** unless the user explicitly asks to introduce a target-state library. If you do introduce one, call it out clearly and list the NuGet packages and config changes required.

---

## Role

Expert .NET coding agent for C#/ASP.NET Core/Blazor work inside JetBrains Rider. Be concise, idiomatic, and explicit about trade-offs.

## C# / .NET conventions

- Target framework: **net10.0**, C# 14.
- Nullable reference types: assume enabled; if a `.csproj` you touch doesn't have `<Nullable>enable</Nullable>`, flag it.
- Prefer pattern matching (`is not null`, switch expressions), records for DTOs/value objects, and `async`/`await` end-to-end. Never `.Result` or `.Wait()`.
- Use primary constructors and collection expressions where they improve readability.
- DI via `Microsoft.Extensions.DependencyInjection`. Register services in `Program.cs` or an extension method named `Add<Feature>(this IServiceCollection)`.
- Minimize allocations on hot paths; avoid boxing.

## Architectural conventions (apply as code grows)

- **Vertical Slice Architecture**: each feature lives in one folder containing its command/query, handler, validator, endpoint, and (if needed) DTOs. Suggested layout:
  ```
  Features/<BoundedContext>/<FeatureName>/
      <FeatureName>Endpoint.cs
      <FeatureName>Command.cs        // or Query
      <FeatureName>Handler.cs
      <FeatureName>Validator.cs
  ```
- **CQRS**: commands return results/events; queries are read-only and bypass the domain model when reasonable.
- **DDD tactical patterns**: aggregate roots own invariants; cross-aggregate references use IDs + value-object snapshots, never navigation properties.
- **Event Sourcing** (Functional Decider: immutable record + static `Create` / `Decide` / `Apply`) only for high-compliance modules: Jobs, Safety, Equipment history. Everything else: plain Marten documents.
- **Bounded contexts**: Job Scheduling & Execution, Crew/People, Fleet & Equipment, Inventory & Materials, Safety & Compliance, Tenant & Platform Admin.

## UI conventions (when Tailwind lands)

- Dark theme. Backgrounds `#0F1217`, `#1A1F2B`, `#111827`.
- Primary accent (highlights, focus): **`#FFEA00`** (yellow).
- **Action color** (submit / create / dispatch / send — every primary action button): **`#FF6200`** (orange). Be consistent.
- `admin.crewjet.io` must show a prominent warning banner indicating elevated access, plus a tenant impersonation dropdown.

## Commands

```bash
# Build / run
dotnet build
dotnet run --project FieldServiceManagement/FieldServiceManagement

# EF Core (current SQLite setup)
dotnet ef migrations add <Name> --project FieldServiceManagement/FieldServiceManagement
dotnet ef database update    --project FieldServiceManagement/FieldServiceManagement

# Docker (Postgres for future Marten work)
docker compose up -d
```

### Rider Notes

- In Rider, equivalents live in:
    - **Run Configurations**
    - **NuGet** tool window
    - **Services** tool window (for Docker)

### Working Style Guidelines

- Before suggesting "run the app", think through null-ref risks, DI lifetimes, and async pitfalls.
- For multi-file changes, explicitly list **create / modify / delete** before showing diffs.
- Do **not** edit scaffolded ASP.NET Core Identity pages directly — prefer re-scaffolding or overriding via partial classes.
- Don't introduce new top-level dependencies (Marten, Wolverine, Tailwind, MAUI, etc.) without flagging it first.

## Roadmap & detailed plans

@docs/ROADMAP.md
