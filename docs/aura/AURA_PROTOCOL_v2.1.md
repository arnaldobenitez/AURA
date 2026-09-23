```text
STATUS: OBSOLETE
SUPERSEDED BY: AURA_PROTOCOL v2.2 (docs/aura/AURA_PROTOCOL_v2.2.md)
```

# AURA_PROTOCOL v2.1

```text
ProtocolName:   AURA_PROTOCOL
Version:        2.1
Status:         IMPLEMENTED_PENDING_PRODUCTION_VERIFICATION
PublishedAt:    2026-08-26
Compatibility:  All agents (ANALYST, CODING_AGENT, AUTONOMOUS_AGENT), all AURA-consumer projects
Supersedes:     rules/AURA.md @ 2026-08-16.2
                docs/aura/AURA_PROTOCOL_v2.md @ 2.0
```

This document is the single full specification of AURA_PROTOCOL v2.1 — an additive extension
of [AURA_PROTOCOL_v2.0](AURA_PROTOCOL_v2.md) built under task `AURA-PROTOCOL-V2-01` to close the
blockers v2.0's own audit found: no authentication, no per-workstream concurrency control, no
credential lifecycle. Everything v2.0 already specified (roles, event types, EXEC/PROJECT
distinction, READ-BACK, contingency) still applies unchanged; this document only adds what's new
and calls out where behavior changed.

## 1. What changed vs. v2.0 (summary)

| Area | v2.0 | v2.1 |
|---|---|---|
| Authentication | None — every endpoint anonymous | `POST /api/project-control/events` and `POST /api/project-control/projects` require a JWT (§2–§3). GETs require one too, since 2026-08-28 (§12) — originally shipped anonymous (§9 as first written), changed within days at explicit request. |
| Actor identity | Trusted from the request body | Derived from the authenticated JWT; body must agree or the request is rejected (§4). |
| Concurrency granularity | One `CONTROL` per `(tenantId, projectCode)` | One lease per `(tenantId, projectCode, workStream)` — independent workstreams never block each other (§5, §6). |
| Optimistic concurrency | `expectedPreviousItemId` only | Same, plus `LeaseId`/`ExpectedLeaseVersion` for the exclusive-work events (§6). |
| `WORKSTREAM` | Documented as absent | Real, optional field (§5). |

## 2. Credentials

`AgentCredential` (new table, additive) is the only place a machine or scripted-human identity
is registered: `ActorType` (`HUMAN`, `AI_ANALYST`, or `CODER` — never `UNASSIGNED`), `AgentName`,
an optional `LinkedActorId` (ties the credential to a specific `Usuarios`/`ProCodificadores`/
`ProProyectosAnalistas` row when that level of pinning is wanted), and a salted PBKDF2-SHA256
hash of the secret (210,000 iterations) — **the plaintext secret is returned exactly once, at
creation or rotation, and never stored or shown again.**

Administration is a protected endpoint set, not a full UI (deliberately — see §10):

- `POST /api/admin/agent-credentials` — create; returns `{ credentialId, secret }` once.
- `POST /api/admin/agent-credentials/{id}/rotate?tenantId=n` — revokes the old one, creates a
  replacement with the same identity, returns its new secret once.
- `POST /api/admin/agent-credentials/{id}/revoke?tenantId=n` — revokes; a revoked credential can
  never mint a new token again.
- `GET /api/admin/agent-credentials?tenantId=n` — lists metadata only (`createdAtUtc`,
  `revokedAtUtc`, `lastUsedAtUtc`) — never a secret.

All four require the `X-Admin-Api-Key` header (a single static key, `Admin:ApiKey` in
configuration — same never-in-appsettings.json convention as everything else secret in this
repo). This exists specifically because bootstrapping the very first credential cannot itself
require a JWT.

## 3. Token issuance

`POST /api/auth/token` — anonymous (the credential's secret *is* the authentication), body
`{ tenantId, credentialId, secret }`. On success: `{ accessToken, expiresAtUtc }`, a JWT valid
for `Jwt:AccessTokenLifetimeMinutes` (15 by default, config, not secret). On failure: `401` with
no detail beyond "invalid credential" or "revoked" — never reveal which part was wrong.

The JWT is signed HMAC-SHA256 with `Jwt:SigningKey` — a real secret, configured exactly like
`ConnectionStrings:DefaultConnection` (User Secrets in development, hand-edited into the
deployed `appsettings.json` in production; never committed). Its claims:
`aura:tenantId`, `aura:credentialId`, `aura:actorType`, `aura:agentName`, and (if pinned)
`aura:linkedActorId` — see `AURA.Application.AgentCredential.AuraJwtClaimTypes`.

## 4. Authorization (identity is authenticated, not asserted)

Every `POST /api/project-control/events` derives the caller's `ActorType` from the JWT and
compares it against the request body's `actor.type`:

- **Mismatch → `403 Forbidden`.** A `CODER` credential can never register an event whose body
  claims `actor.type = HUMAN` — this is what makes the pre-existing "`REQUEST`/`APPROVAL` require
  `HUMAN`" rule actually enforceable instead of merely declarative.
- If the credential has a pinned `LinkedActorId`, `actor.id` in the body must equal it or the
  request is rejected the same way. Unpinned credentials (the common case for `CODER`/
  `AI_ANALYST`, and acceptable for `HUMAN` too — see §8) accept whatever `actor.id` the body
  asserts, since that id is a display label today, not an access-control boundary.
- The JWT's `tenantId` must equal the request's `tenantId` — a token minted for one tenant can
  never act on another, even if it names a project code that happens to exist there too.

## 5. WORKSTREAM

`workStream` (optional string, ≤ 100 chars) now exists on the wire (`RegisterProjectEventRequest
.WorkStream`) and in the schema (`ProProyectoEXEC.sWorkStream`, `ProProyectosItems.sWorkStream`).
`null`/absent means "the whole project," identical to v2.0's behavior — this is why every
pre-existing test and integration kept working unmodified. A project that sends distinct
`workStream` values gets fully independent EXEC/stage continuity, control, and lease per value:

```text
Project A / Workstream X / Task 1 → Agent A   (its own lease, EXEC, stage chain)
Project A / Workstream Y / Task 2 → Agent B   (its own lease, EXEC, stage chain - never touches X)
```

Physical `IdItems`/`IdEXEC` numbering stays project-wide (not per-workstream) to avoid a second
identity axis on those primary keys — only *which* row counts as "current" for chain/lease
purposes is workstream-filtered. See `SqlProjectControlRepository.GetChainStateAsync`.

## 6. Lease and optimistic concurrency

The lease governs **exclusive work** — `CLAIM`, `PROGRESS`, `HANDOFF`, `BLOCKER`, `DEPLOYMENT`.
`REQUEST`, `APPROVAL`, and `REWORK` are HUMAN governance actions layered on top and do not
require holding it (see §8 for why). `COMPLETION` releases it.

- **`CLAIM`** acquires or renews it: if no lease is active for `(tenantId, projectCode,
  workStream)`, or the active one is already this same authenticated credential's, a fresh lease
  is issued (`LeaseId` — new GUID, `Version = 1`, `ExpiresAtUtc = now + AgentLease:
  DurationMinutes`). If a *different* credential holds an unexpired lease → `409 LEASE_CONFLICT`.
- **`PROGRESS`/`HANDOFF`/`BLOCKER`/`DEPLOYMENT`** must include the `LeaseId` and
  `ExpectedLeaseVersion` they were last given. The server checks, in order: the lease is still
  active (else `409 LEASE_CONFLICT`, "lease vencido"); `LeaseId`+`ExpectedLeaseVersion` match the
  current one exactly (else `409 LEASE_CONFLICT`, stale version — re-read and retry); the caller
  is the current holder (see below). On success the lease is renewed (`Version + 1`, extended
  expiry) — this is the "heartbeat," there is no separate renew-only endpoint in this pass.
- **`HANDOFF`** additionally reassigns the lease atomically to the declared `nextControl`
  (fresh `LeaseId`, `Version = 1`) in the same transaction as the event insert — no window where
  the old and new holder could both believe they have control.
- **Holder identity is the authenticated credential**, not the free-text `actor.type`/`actor.id`
  in the body — this is what lets, e.g., a `HUMAN`-type credential shared across many display
  names still be recognized as "the same holder" across a `PROGRESS`→`APPROVAL`→`PROGRESS`
  sequence, while a genuinely different credential of the same declared type is correctly
  refused. The one exception: immediately after a `HANDOFF`, the new holder's credential isn't
  known yet (they haven't authenticated under this lease), so the very next accepted write from
  whoever matches the *declared* `nextControl` type/id locks in their credential from then on.
- Response bodies (`ProjectControlEventResponse.lease`) always carry the current `leaseId`,
  `version`, `expiresAtUtc`, and holder identity after a successful write (except `REQUEST`/
  `COMPLETION`, which return `null` — they don't hold one) — this is what a client threads into
  its next call, exactly like it already threads `expectedPreviousItemId`.

## 7. Idempotency (unchanged, verified against the new logic)

`eventId` idempotency is checked **before** lease resolution — a retried `eventId` returns
`200 ALREADY_EXISTS` without re-validating or re-bumping the lease, so retries of `CLAIM`,
`PROGRESS`, `COMPLETE`/`COMPLETION`, `BLOCKER`, and `HANDOFF` are all safe.

## 8. HUMAN vs. ANALYST — audited, not changed automatically

Per this task's explicit instruction, the pre-existing restriction (`REQUEST`/`APPROVAL` require
`actor.type = HUMAN`) was **not** removed or loosened. What changed is that it is now actually
enforced against an authenticated identity instead of a self-reported one (§4) — closing the
real risk this task called out explicitly: **an AI could not previously be stopped from
asserting `actor.type = HUMAN` and self-approving its own work; it now cryptographically can
be.** `REWORK` remains open to any actor type, matching the pre-existing (v2.0) behavior — this
task did not find evidence that restricting it further was already decided, so it was left alone
rather than tightened unilaterally.

**Operations that must stay HUMAN:** `REQUEST` (opens an EXEC), `APPROVAL` (closes a stage).
**Operations any authenticated role may perform:** `CLAIM`, `PROGRESS`, `HANDOFF`, `BLOCKER`,
`REWORK`, `COMPLETION`, `DEPLOYMENT`.
**UX impact:** an `AI_ANALYST` or `CODER` that needs a new EXEC opened or a stage approved must
hand off to a `HUMAN` for exactly that action — it cannot perform it itself, by design.
**Risk addressed:** self-approval by an AI is now blocked at the authentication layer, not just
by convention.

## 9. Scope decisions made explicitly (not silent gaps)

- **Read endpoints stay anonymous.** *(Superseded 2026-08-28 — see §12: a basic web login was
  explicitly requested and now gates reads too. Left here as the original reasoning for why they
  started anonymous.)* Locking down `GET /api/project-control` and
  `GET /api/project-control/projects/{code}/events` would break the existing, already-deployed
  `AURA.Web` board (which has no login) and was not part of what this task's audit identified as
  the write-side risk. Flagged, not fixed, this pass.
- **No per-project ACL.** A credential is scoped to a tenant, not to a specific set of projects
  within it — there is no `AgentCredentialProject` allowlist. Adding one would be a third schema
  decision beyond the two gates this task's design phase raised (`WORKSTREAM`, credential
  storage); it was not requested and was not added silently.
- **`LinkedActorId` is optional even for `HUMAN` credentials**, not mandatory. Making it
  mandatory would have required either a dedicated credential per distinct human id (impractical
  given how many ad-hoc human ids the existing system already uses for display purposes) or a
  weaker mid-tier scheme; the credential's authenticated identity (not the declared `actor.id`)
  is what actually gates access, so this is a deliberate, documented simplification, not an
  oversight.
- **No explicit `/leases/renew` endpoint.** Any accepted write already renews the lease
  (heartbeat-by-write). A pure "keep-alive with nothing to say yet" endpoint is a reasonable
  future addition if a long-running agent needs it, not added this pass to keep the surface
  proportionate to what was asked.

## 10. Credential administration — why not a full UI

The task explicitly permits this: "si una UI completa no es necesaria para activar v2.0/v2.1,
puede existir inicialmente una operación administrativa segura/documentada." A small,
`X-Admin-Api-Key`-protected endpoint set (§2) satisfies create/revoke/rotate/list without
building account management, roles, or a web UI that nothing else in AURA has yet either.

## 11. Production activation gate

This document's `Status` cannot become `ACTIVE` until, per this task's own closing criteria:
security is implemented (done), tests pass (done, locally — §V of the final report), a real
deployment to `https://aura.aguarapp.com` has happened, a controlled production smoke test has
passed there, and its read-back has been confirmed. The deployment step is a manual operation on
host OMEN that this agent has no access to perform — see the final report's section S/T for what
is and is not possible to complete without that manual step.

## 12. Addendum (2026-08-28): basic human web login, reads now also gated

A follow-up request asked for a quick login gate on the `AURA.Web` dashboard itself: "solo
permitir visualizar si el login es correcto" (only allow viewing if login is correct). This
directly supersedes §9's "read endpoints stay anonymous" decision — `GET /api/project-control`
and `GET /api/project-control/projects/{code}/events` now require the same JWT bearer scheme as
writes (`[Authorize]` moved to the controller level).

**What was added, additively, on top of everything above:**

- `Usuarios` gained `sEmail`, `sPasswordSalt`, `sPasswordHash`, `nPasswordIterations` (nullable —
  existing human rows without a login configured are untouched and simply can't log in).
- `POST /api/auth/login` (anonymous, like `/api/auth/token`): `{ email, password }` → the same
  kind of JWT an `AgentCredential` gets (`ActorType = HUMAN`, `LinkedActorId = IdUsuario`), via
  the same `IAccessTokenIssuer`. Tenant is hardcoded to `1` — this is a quick access gate, not a
  multi-tenant login system, per the request ("smok poner tenant 1").
- Default owner seeded once (tenant 1, `IdUsuario = 1`, name "Arnaldo Benitez"), never
  overwritten by a later migration run if the email is already set (see
  `ProjectControlSchemaMigrator.SeedDefaultHumanLoginAsync`).
- `AURA.Web`: a login screen gates the whole app; the resulting token is attached to every API
  call and a `401` clears it and returns to the login screen.

**What was explicitly not built, by request** ("no controlar qué se pueda hacer por dentro por
ahora"): no roles or permissions beyond "a valid login exists" — any authenticated human sees
everything the dashboard shows, identical to before this change. `IdUsuario = 1` is described as
"administrador owner" conceptually, but nothing in code currently treats it differently from any
other logged-in user — there is exactly one seeded account today, so the distinction has no
observable effect yet. If per-user permissions are wanted later, that is new, separate work.

## 13. AURA Project Memory

`AURA_PROJECT_MEMORY v1.0` is the portable, local documentation layer for projects that adopt AURA. Its normative specification and reusable contracts live in `docs/aura/AURA_PROJECT_MEMORY_v1.0.md` and `docs/aura/project-memory/`.

Project Memory and this operational protocol are complementary but independent:

- `AURA_PROTOCOL` defines operational authority, authentication, leases, events, control, operational state and required read-back.
- Project Memory defines portable context, consolidated documentation, decisions, backlog, architecture and semantic session history.

Before a relevant task, an agent may load Project Memory when the working project contains `.aura/project.json`: resolve `PROJECT_ROOT`, read `AURA.md`, the manifest, `docs/00-PROYECTO.md` and `docs/01-ESTADO-ACTUAL.md`, then load optional modules progressively. At the end of a relevant session, it should update local documentation according to Project Memory.

Updating Project Memory does **not** constitute an AURA event, acquire a lease, grant authority, or prove an operational state change. A local session may be recorded while the API is unavailable, but the agent must not claim an event, lease, read-back or operational confirmation that was not actually verified. When the two planes disagree, operational authority remains governed by this protocol and its official state source; the discrepancy must be documented and reconciled without silently overwriting either plane.

Project Memory never changes the authority restrictions of this protocol and does not require API access, Git, or a specific agent.

