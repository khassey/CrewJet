# CrewJet — Roadmap & Detailed Plans

This document holds the longer-form roadmap and modeling notes that don't need to be in every Claude Code turn. It's imported on demand from `CLAUDE.md` via `@docs/ROADMAP.md`.

---

## Foundational Pillars — Prioritized Order

### 1. Security Foundations
- Microsoft 365 OIDC + tenant resolution by subdomain
- Authorization (roles/claims per tenant)
- SCIM provisioning endpoints
- Tenant impersonation (for super admins)

### 2. Multi-Tenancy Infrastructure
- Subdomain tenant resolution middleware
- Tenant-aware Marten configuration
- Data isolation guarantees
- Admin tenant impersonation feature

### 3. Auditing
- Immutable audit trail (especially critical for jobs and safety)

### 4. Logging, Monitoring, and Observability
- Structured logging (Serilog), OpenTelemetry, Wolverine + Marten metrics

### 5. Critter Stack Core Setup
- Wolverine + Marten configuration
- Selective Event Sourcing strategy
- Vertical slice folder structure

### 6. UI Foundations
- Tailwind CSS setup in Blazor
- Custom component library (MultiSelect, Calendar/Scheduler)
- Design system based on CrewJet mock (dark theme, yellow accents, **orange for all actions**)

### 7. Resiliency / Fault Tolerance
- Retries, circuit breakers, Wolverine middleware

### 8. Data Management and Schema Evolution
- Versioned changes, soft deletes, zero-downtime strategies

---

## Critter Stack Early Foundations Checklist

### Phase 1 — Core Infrastructure (Weeks 1–3)
- [ ] Wolverine + Marten setup (transactional outbox, multi-tenancy)
- [ ] Tenant resolution middleware + authentication pipeline
- [ ] Vertical Slice structure and naming conventions
- [ ] Tailwind + base Blazor component library + action button styling

### Phase 2 — Domain & Modeling Standards (Weeks 2–5)
- [ ] Aggregate / Decider pattern standards (Event Sourcing for critical modules only)
- [ ] CQRS command/query/handler conventions
- [ ] Ubiquitous Language glossary

### Phase 3 — Quality & Observability
- [ ] Observability, correlation IDs
- [ ] Testing foundations (bUnit, Wolverine test support, Marten fixtures)
- [ ] Custom MultiSelect and Calendar components

### Phase 4 — Operations Readiness
- [ ] SCIM provisioning
- [ ] `admin.crewjet.io` portal with impersonation dropdown + elevated access warning
- [ ] Deployment pipelines on Fly.io (Blue/Green where possible)

---

## Detailed DDD & Modeling Decisions

### Bounded Contexts
- Job Scheduling & Execution
- Crew / People Management
- Fleet & Equipment Management
- Inventory & Materials
- Safety & Compliance
- Tenant & Platform Administration

### Aggregate Root Strategy
- `Job`, `Crew`, `Vehicle`, `Employee`, etc. as separate Aggregate Roots.
- Cross-aggregate references use Identity + Value Objects with snapshot data (no EF navigation properties between aggregates).

### Event Sourcing Strategy
- **Functional Decider pattern**: immutable record + static `Create`, `Decide`, and `Apply` methods.
- Applied only to high-compliance modules: Jobs, Safety, Equipment history.
- Other modules use plain Marten documents to reduce ceremony.

### Custom UI Components Needed
- Advanced multi-select component
- Calendar / Scheduler component (heavily used for dispatch and crew scheduling)

---

## Authentication & Tenant Strategy

- **Primary Auth**: Microsoft 365 (OIDC)
- **Provisioning**: SCIM endpoints implemented in-house for client user sync
- **Tenant Resolution**: Subdomain-based (`https://acme-electric.crewjet.io`)
- **Roles**:
    - Platform Super Admin on `admin.crewjet.io` (tenant impersonation capability)
    - Client-level Admin / Manager on client subdomains

---

## Mobile Strategy

- Future: .NET MAUI Blazor Hybrid app to reuse Blazor components for field technicians.

---

## Development Context

- Solo developer (Kelly) using AI assistance + Vertical Slice Architecture to accelerate delivery.
- Close collaboration with Product Owner / BA / COO who will run event-storming sessions with the electrician business client.
- Focus on rapid, high-quality vertical slices, especially the Job workflow.

---

## Current Phase

Early foundation stage. Priority: solid multi-tenant authentication, core infrastructure (Wolverine + Marten), Tailwind-based UI system (with consistent orange action styling), and the first high-value Job vertical slice.