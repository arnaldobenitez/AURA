# AURA — Agent Integration Guide

Companion to [AURA_PROTOCOL_v2.2.md](AURA_PROTOCOL_v2.2.md) for `GOVERNED_EXECUTION` and
[AURA_AGENT_REGISTRATION_PROTOCOL_v2.3.md](AURA_AGENT_REGISTRATION_PROTOCOL_v2.3.md) for the
additive `REGISTRATION_ONLY` channel. This document is the practical "how do I actually call
this" reference; the protocol documents remain the normative specifications. If they ever
disagree, this document is wrong and must be fixed to match them.

## 1. Minimum an agent needs to know

- `projectCode` for the project it is working on, `tenantId` (production tenant is `1`), and —
  since v2.1 — an `AgentCredential` (`credentialId` + secret) an Analyst/admin provisioned for it
  (see §2). There is no self-registration; credential creation is an administrative action.
- Which mode it is using. Governed work still uses one of the three roles `ANALYST`,
  `CODING_AGENT`, or `AUTONOMOUS_AGENT` (see `AURA_PROTOCOL_v2.md` §3) — this is enforced, not
  just declared: your credential's `ActorType` (`HUMAN`, `AI_ANALYST`, or `CODER`) is checked
  against every governed event's `actor.type` and the request is rejected on mismatch
  (`AURA_PROTOCOL_v2.2.md` §3). A technical `INGESTION` credential is only for v2.3
  `REGISTRATION_ONLY` writes and read-back; it cannot claim, hold a lease, approve, or alter
  governed execution state.
- Its own stable identity: `agent`, `model`, and `environment` strings — required for `CODER`/
  `AI_ANALYST` actors, free text (no fixed registry of allowed agent names).
- Optionally, a `workStream` if the project has independent concurrent units of work
  (`AURA_PROTOCOL_v2.2.md` §4) — omit it for a project with none.

## 2. Getting a token

```text
1. Un administrador humano autenticado crea la credencial desde Administración → Credenciales de agentes,
   o mediante POST /api/admin/agent-credentials con JWT portal `PORTAL_LOGIN` + rol configurado.
   Para `INGESTION`, el request debe incluir tenantId, actorType, agentName y uno o más projectIds.
   La respuesta incluye { credentialId, secret } una sola vez. Guardalos en el secret manager del agente.
   `X-Admin-Api-Key` se conserva solo para integraciones backend; nunca se configura en Devin/browser.
2. El agente intercambia su credencial por un JWT corto:
   POST /api/auth/token
   body: { tenantId, credentialId, secret }
   -> { accessToken, expiresAtUtc }
3. Cada escritura lleva Authorization: Bearer <accessToken>. AURA vuelve a comprobar la asignación
   proyecto-credencial en cada evento; el scope no se confía únicamente a los claims del JWT.

Variables sugeridas para Devin: `AURA_INGESTION_CREDENTIAL_ID`, `AURA_INGESTION_SECRET`,
`AURA_TENANT_ID` y `AURA_API_BASE_URL`. El secreto plaintext se guarda solo en el gestor secreto del agente.
```

Tokens expire (`Jwt:AccessTokenLifetimeMinutes`, 15 min by default) — request a new one when a
write starts failing with `401`. There is no refresh-token flow in this pass; re-authenticate
with the same credential/secret.

## 3. Endpoints

| Purpose | Method + path | Auth |
|---|---|---|
| Protocol discovery | `GET /api/aura-protocol` | none |
| Board (project state) | `GET /api/project-control?tenantId={n}` | Bearer JWT (since 2026-08-28) |
| Project history | `GET /api/project-control/projects/{projectCode}/events?tenantId={n}` | Bearer JWT (since 2026-08-28) |
| Get access token (agent) | `POST /api/auth/token` | none (credential+secret in body) |
| Get access token (human) | `POST /api/auth/login` | none (email+password in body) |
| Create a project | `POST /api/project-control/projects` | Portal `PORTAL_LOGIN` JWT + tenant-admin allowlist |
| Register a governed event | `POST /api/project-control/events` | Bearer JWT (`HUMAN`/`AI_ANALYST`/`CODER`) |
| Register an append-only event (v2.3) | `POST /api/agent-registration/events` | Bearer JWT (`INGESTION`) |
| Read back a registration event | `GET /api/agent-registration/events/{eventId}?tenantId={n}` | Bearer JWT (`INGESTION`, same tenant + assigned project scope) |
| Admin: projects, credentials and project scopes | `/api/admin/projects...`, `/api/admin/agent-credentials...` | Portal JWT `PORTAL_LOGIN` + tenant-admin allowlist; `X-Admin-Api-Key` backend-only |
| Liveness | `GET /health` (no DB dependency) | none |
| Readiness | `GET /health/ready` (depends on SQL Server) | none |
| OpenAPI contract (Development only) | `GET /openapi/v1.json` | none |

Roadmap endpoints: `GET /api/project-control/projects/{projectCode}/roadmap?tenantId={n}[&workStream=...]` reads ordered stage state; `PUT /api/project-control/projects/{projectCode}/roadmap` defines or updates the plan and requires a HUMAN credential.

Base URLs: `http://localhost:5235` in development, `https://aura.aguarapp.com` in production
(mounted at `/api` on IIS). Never assume `localhost` — check which environment you are actually
targeting.

The Roadmap read endpoint returns the ordered stages for the active EXEC/workStream. A HUMAN credential configures or updates the plan using `PUT /api/project-control/projects/{projectCode}/roadmap` with `tenantId`, `workStream`, a new `eventId`, and ordered `stages[]` (`stageCode`, `description`, `order`, optional `acceptanceCriteria`, and active `AI_ANALYST` `validatorCredentialId`). The write is idempotent and audited; existing stages cannot be removed, and started stage definitions are locked.

## 4. Minimal working sequence

```text
1. POST /api/auth/token                                        -> get a Bearer token
2. GET  /api/aura-protocol                                     -> confirm protocol version/status
3. GET  /api/project-control?tenantId={n}                      -> find your project and read LastItemId/CurrentControl
4. GET  /api/project-control/projects/{code}/roadmap?tenantId={n} -> read ordered stages, criteria and assigned validator
5. POST /api/project-control/events { eventType: CLAIM, ... }    -> take control; response.lease has LeaseId/Version
6. PROGRESS at verified milestones with stageCode/stageProgress and the latest lease; READ-BACK after every write
7. HANDOFF to the assigned AI_ANALYST when stage progress reaches 100%; do not self-approve
8. Designated AI_ANALYST posts APPROVAL with stageCode, PASS/PASS_WITH_WARNINGS, summary and evidence
9. GET Roadmap and board                                             -> READ-BACK before reporting success
```

## 5. The lease (introduced in v2.1)

`CLAIM` and `REQUEST` never send `leaseId`/`expectedLeaseVersion` (they establish/don't touch
one). Every `PROGRESS`, `HANDOFF`, `BLOCKER`, and `DEPLOYMENT` **must** include the `leaseId` and
`expectedLeaseVersion` from the previous response's `lease` object, or the request is rejected
with `400 VALIDATION_FAILED`. `REQUEST`/`APPROVAL`/`REWORK` don't need one (they're HUMAN
governance actions, not part of the exclusive-work lease — see `AURA_PROTOCOL_v2.2.md` §5, §7).

```json
{ "leaseId": "…", "version": 3, "expiresAtUtc": "…", "holderActorType": "CODER", "holderActorId": 1 }
```

If someone else holds it, or your version is stale, or it expired: `409` with
`"status": "LEASE_CONFLICT"` — re-`GET` the board or re-`CLAIM` and retry.

## 6. Registration-only mode (v2.3)

Use `POST /api/agent-registration/events` only when the goal is durable project activity
registration, not governed control. The request must include `mode: "REGISTRATION_ONLY"`,
`eventId`, `tenantId`, `projectCode`, one of `INFO`/`PROGRESS`/`DECISION`/`BLOCKER`/`HANDOFF`/
`REQUEST`/`NOTE`/`ERROR`, `summary`, and `occurredAtUtc`. Declared actor fields are stored as
`UNVERIFIED`; they are evidence metadata, not authorization.

A repeated `eventId` returns `200 ALREADY_EXISTS` with the original `itemId`. A new
`PROJECT_SCOPED` credential can write only to an active project assigned to it; an unknown,
paused, archived or unassigned project returns `403` and no row is created. Create the project in
AURA Administration first and copy its canonical `projectCode`. The provisional resolution remains
only for explicitly marked legacy `LEGACY_TENANT_WIDE` credentials; it does not create a governed
project or authorization. Registration never changes the board, current
control, Roadmap, EXEC, workStream, leases, approvals, or execution status.

Read-back is `GET /api/agent-registration/events/{eventId}?tenantId={n}` with an `INGESTION`
token for the same tenant. For the full request/response and safety rules, see
[AURA_AGENT_REGISTRATION_PROTOCOL_v2.3.md](AURA_AGENT_REGISTRATION_PROTOCOL_v2.3.md).

## 7. Security caveats (read this before integrating anything)

- **Portal administration is not implied by a valid login.** Admin endpoints require a signed
  human portal-login JWT (`aura:authSource=PORTAL_LOGIN`) plus an explicit tenant/user allowlist
  (`Admin:TenantAdminUserIds:<tenantId>`). An AgentCredential of type HUMAN and all agent
  credentials are not portal-admin sessions.
- `X-Admin-Api-Key` is a privileged server-to-server compatibility path only. Never put it in
  frontend config, Devin secrets or browser requests.
- Never place secrets (connection strings, tokens, credential secrets) in any field of the event
  contract, or in `agentName`/`reference`/free-text fields. A credential's secret is shown once,
  at creation/rotation — store it the way you'd store any API key, never in a prompt or repo.
- CORS only allows explicit loopback origins, and only in `Development` — a browser-based
  integration hitting production directly from a third-party origin will be blocked by design;
  server-to-server calls are unaffected by CORS.

## 8. Contract validation before writing

Before your first `POST`, fetch `GET /openapi/v1.json` (Development) or read
`RegisterProjectEventRequest` directly in `src/AURA.Application/ProjectControl/` and confirm the
fields you plan to send actually exist in the deployed version. A `201`/`200` response does
**not** prove an unrecognized field was persisted — only a read-back that shows the value you
expect is proof.

## 9. Field-level rules that will otherwise surprise you

- `summary` is required, ≤ 300 chars. `details`, `nextAction`, `blocker`, `reference` ≤ 1000
  chars. `stage`, `environment`, `agent`, `model`, `workStream` ≤ 100 chars.
- `stageProgress` must be `0`–`100` with at most two decimals, or omitted.
- `REQUEST` and a legacy `APPROVAL` without `stageCode` require `actor.type = HUMAN`. A Roadmap `APPROVAL` with `stageCode` instead requires the assigned `AI_ANALYST` credential. The authenticated JWT identity is enforced; a caller cannot self-assert another actor type.
- `actor.type = CODER` or `AI_ANALYST` **requires** non-empty `agent`, `model`, and
  `environment` — `400 VALIDATION_FAILED` if one is missing.
- `PROGRESS`, `HANDOFF`, `BLOCKER`, and `DEPLOYMENT` require `leaseId` + `expectedLeaseVersion` (§5). `APPROVAL`, `REWORK`, `ROADMAP` plan management and `COMPLETION` do not.
- Roadmap stages require a `stageCode`; `stageProgress` is `0`–`100`. Only the assigned AI_ANALYST credential may send `APPROVAL` for that Roadmap stage, with `stageOutcome = PASS` or `PASS_WITH_WARNINGS`, after progress reaches `100`. A CODER or a different validator is rejected. Legacy stages without a Roadmap code retain HUMAN approval.
- `PASS_WITH_WARNINGS` counts as a closed milestone in global progress, with the warning outcome/evidence retained.
- `COMPLETION` is rejected unless all stages of the active EXEC are approved.
- A `CLAIM` ignores any `nextControl` sent alongside it — the claimant becomes control,
  regardless of what `nextControl` says. Do not rely on combining `CLAIM` with a redirect.

## 10. Response codes

| Status | Meaning |
|---|---|
| `201 Created` | New item registered. |
| `200 ALREADY_EXISTS` | Retry with an `eventId` already seen — treat as success, not an error. |
| `409 CONFLICT` | `expectedPreviousItemId` is stale — re-`GET`, retry with the same `eventId` and the corrected id. |
| `409 LEASE_CONFLICT` | Someone else holds the lease, your version is stale, or it expired — re-`GET`/re-`CLAIM`. |
| `403` | Authenticated, but the credential's role/tenant doesn't match what the request asserts. |
| `401` | Missing/invalid/expired Bearer token, or invalid/revoked credential at `/api/auth/token`. |
| `400 VALIDATION_FAILED` | Contract or business-rule violation (see §8) — body includes the specific error list. |
| `404` | Unknown `(tenantId, projectCode)` on governed endpoints, or missing `eventId` on registration read-back. |

## 11. AURA Project Memory

When the working directory belongs to a project containing `.aura/project.json`, load Project Memory independently of the operational AURA API:

1. Resolve `PROJECT_ROOT` using the nearest valid `.aura/project.json`.
2. Read `AURA.md`, the manifest, `docs/00-PROYECTO.md`, and `docs/01-ESTADO-ACTUAL.md`.
3. Load optional modules only when the task needs them.
4. Determine the operational role and continue with this protocol's authority, authentication, lease, event, and read-back rules.
5. For a relevant session, update consolidated local documents and append one semantic session record; never overwrite history.

Project Memory is not an AURA event. A document update does not prove an event, lease, handoff, or read-back. If the API is unavailable, the agent may read and update local Project Memory and record the limitation, but must not claim operational synchronization or invent confirmation. If Project Memory is incomplete, classify the condition as `ERROR` for missing/invalid Core, `WARNING` for a declared optional module that is absent, or `INFO` for an absent `AGENTS.md`/Git/API; continue only when the task can be performed safely.

If local documented state conflicts with operational state, use the official AURA operational source for authority and control, retain the local documentation as evidence of the documented state, and record the discrepancy for reconciliation. Neither plane silently overwrites the other.

## 12. Capability negotiation (what to do if you can't do all of this)

If your environment cannot make outbound HTTP calls at all (conversational-only access), you
cannot participate in AURA's live protocol. Say so explicitly rather than fabricating a
synchronized state. Do not attempt to use SQL directly as a substitute under any circumstance —
this is an explicit rule of the protocol (`AURA_PROTOCOL_v2.md` §14), not just a suggestion.

## 13. Interoperability dogfood — Etapa 7B

Se realizó una prueba real de interoperabilidad entre agentes sobre este repositorio. El agente
resolvió `PROJECT_ROOT`, ejecutó el bootstrap estructurado antes de modificar documentación y
obtuvo `project.valid = true`, `projectId = ba3a550e-2304-4b95-b742-5fff7ece99d7`, Core válido,
sesiones locales vacías y contexto Git disponible. Luego abrió una sesión, actualizó el estado
consolidado y cerró la sesión mediante el flujo Project Memory.

La prueba confirma que otro agente puede continuar desde `.aura/project.json`, los documentos Core,
`docs/01-ESTADO-ACTUAL.md` y el registro semántico append-only generado en `docs/historial/`.
No se modificaron código de negocio ni arquitectura. Los módulos opcionales de decisiones,
backlog y arquitectura no están presentes y no fueron creados para esta modificación menor.
