# AURA — Agent Integration Guide

Companion to [AURA_PROTOCOL_v2.1.md](AURA_PROTOCOL_v2.1.md) (which extends
[v2.0](AURA_PROTOCOL_v2.md)). This document is the practical "how do I actually call this"
reference; the protocol document remains the only normative specification. If the two ever
disagree, this document is wrong and must be fixed to match it.

## 1. Minimum an agent needs to know

- `projectCode` for the project it is working on, `tenantId` (production tenant is `1`), and —
  since v2.1 — an `AgentCredential` (`credentialId` + secret) an Analyst/admin provisioned for it
  (see §2). There is no self-registration; credential creation is an administrative action.
- Which of the three roles it is acting as this session: `ANALYST`, `CODING_AGENT`, or
  `AUTONOMOUS_AGENT` (see `AURA_PROTOCOL_v2.md` §3) — this is now enforced, not just declared:
  your credential's `ActorType` (`HUMAN`, `AI_ANALYST`, or `CODER`) is checked against every
  event's `actor.type` and the request is rejected on mismatch (`AURA_PROTOCOL_v2.1.md` §4).
- Its own stable identity: `agent`, `model`, and `environment` strings — required for `CODER`/
  `AI_ANALYST` actors, free text (no fixed registry of allowed agent names).
- Optionally, a `workStream` if the project has independent concurrent units of work
  (`AURA_PROTOCOL_v2.1.md` §5) — omit it for a project with none.

## 2. Getting a token

```text
1. An Analyst/admin creates your credential once:
   POST /api/admin/agent-credentials   (header: X-Admin-Api-Key)
   body: { tenantId, actorType, agentName }
   -> { credentialId, secret }   <-- secret is shown ONLY this once; store it securely
2. You exchange it for a short-lived access token whenever you need one:
   POST /api/auth/token
   body: { tenantId, credentialId, secret }
   -> { accessToken, expiresAtUtc }
3. Every write carries: Authorization: Bearer <accessToken>
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
| Create a project | `POST /api/project-control/projects` | Bearer JWT |
| Register an event | `POST /api/project-control/events` | Bearer JWT |
| Admin: create/rotate/revoke/list credentials | `/api/admin/agent-credentials...` | `X-Admin-Api-Key` |
| Liveness | `GET /health` (no DB dependency) | none |
| Readiness | `GET /health/ready` (depends on SQL Server) | none |
| OpenAPI contract (Development only) | `GET /openapi/v1.json` | none |

Base URLs: `http://localhost:5235` in development, `https://aura.aguarapp.com` in production
(mounted at `/api` on IIS). Never assume `localhost` — check which environment you are actually
targeting.

## 4. Minimal working sequence

```text
1. POST /api/auth/token                                        -> get a Bearer token
2. GET  /api/aura-protocol                                     -> confirm protocol version/status
3. GET  /api/project-control?tenantId={n}                      -> find your project, read LastItemId/CurrentControl
4. POST /api/project-control/events  { eventType: CLAIM, ... }  -> take control; response.lease has LeaseId/Version
5. GET  /api/project-control?tenantId={n}                      -> READ-BACK: confirm CurrentControl is now you
... PROGRESS at real milestones, always sending the last lease.leaseId/version, always reading back after ...
6. POST /api/project-control/events  { eventType: HANDOFF or COMPLETION, ... }
7. GET  /api/project-control?tenantId={n}                      -> READ-BACK: confirm the handoff/close took effect
```

## 5. The lease (new in v2.1)

`CLAIM` and `REQUEST` never send `leaseId`/`expectedLeaseVersion` (they establish/don't touch
one). Every `PROGRESS`, `HANDOFF`, `BLOCKER`, and `DEPLOYMENT` **must** include the `leaseId` and
`expectedLeaseVersion` from the previous response's `lease` object, or the request is rejected
with `400 VALIDATION_FAILED`. `REQUEST`/`APPROVAL`/`REWORK` don't need one (they're HUMAN
governance actions, not part of the exclusive-work lease — see `AURA_PROTOCOL_v2.1.md` §6, §8).

```json
{ "leaseId": "…", "version": 3, "expiresAtUtc": "…", "holderActorType": "CODER", "holderActorId": 1 }
```

If someone else holds it, or your version is stale, or it expired: `409` with
`"status": "LEASE_CONFLICT"` — re-`GET` the board or re-`CLAIM` and retry.

## 6. Security caveats (read this before integrating anything)

- **Writes and reads both require authentication (since 2026-08-28).** `GET /api/project-control`
  and its history endpoint now also need a Bearer token — a basic web login was added on top of
  everything else in this document (see `AURA_PROTOCOL_v2.1.md` §12); the same token from either
  `/api/auth/token` (agents) or `/api/auth/login` (humans) works for both.
- Never place secrets (connection strings, tokens, credential secrets) in any field of the event
  contract, or in `agentName`/`reference`/free-text fields. A credential's secret is shown once,
  at creation/rotation — store it the way you'd store any API key, never in a prompt or repo.
- CORS only allows explicit loopback origins, and only in `Development` — a browser-based
  integration hitting production directly from a third-party origin will be blocked by design;
  server-to-server calls are unaffected by CORS.

## 7. Contract validation before writing

Before your first `POST`, fetch `GET /openapi/v1.json` (Development) or read
`RegisterProjectEventRequest` directly in `src/AURA.Application/ProjectControl/` and confirm the
fields you plan to send actually exist in the deployed version. A `201`/`200` response does
**not** prove an unrecognized field was persisted — only a read-back that shows the value you
expect is proof.

## 8. Field-level rules that will otherwise surprise you

- `summary` is required, ≤ 300 chars. `details`, `nextAction`, `blocker`, `reference` ≤ 1000
  chars. `stage`, `environment`, `agent`, `model`, `workStream` ≤ 100 chars.
- `stageProgress` must be `0`–`100` with at most two decimals, or omitted.
- `eventType = REQUEST` or `APPROVAL` **requires** `actor.type = HUMAN` — rejected from
  `CODER`/`AI_ANALYST` with `400 VALIDATION_FAILED`, and now also unforgeable: your credential's
  authenticated `ActorType` must itself be `HUMAN`, or the request is `403 Forbidden` regardless
  of what the body claims.
- `actor.type = CODER` or `AI_ANALYST` **requires** non-empty `agent`, `model`, and
  `environment` — `400 VALIDATION_FAILED` if one is missing.
- Every event but `CLAIM`/`REQUEST` requires `leaseId` + `expectedLeaseVersion` (§5) —
  `400 VALIDATION_FAILED` if absent.
- `APPROVAL` on a stage whose `stageProgress` is not `100` is rejected as an invalid transition
  (`400`), not silently accepted.
- `COMPLETION` is rejected unless every stage of the current EXEC is already `Approved`.
- A `CLAIM` ignores any `nextControl` sent alongside it — the claimant becomes control,
  regardless of what `nextControl` says. Do not rely on combining `CLAIM` with a redirect.

## 9. Response codes

| Status | Meaning |
|---|---|
| `201 Created` | New item registered. |
| `200 ALREADY_EXISTS` | Retry with an `eventId` already seen — treat as success, not an error. |
| `409 CONFLICT` | `expectedPreviousItemId` is stale — re-`GET`, retry with the same `eventId` and the corrected id. |
| `409 LEASE_CONFLICT` | Someone else holds the lease, your version is stale, or it expired — re-`GET`/re-`CLAIM`. |
| `403` | Authenticated, but the credential's role/tenant doesn't match what the request asserts. |
| `401` | Missing/invalid/expired Bearer token, or invalid/revoked credential at `/api/auth/token`. |
| `400 VALIDATION_FAILED` | Contract or business-rule violation (see §8) — body includes the specific error list. |
| `404` | Unknown `(tenantId, projectCode)`. |

## 10. Capability negotiation (what to do if you can't do all of this)

If your environment cannot make outbound HTTP calls at all (conversational-only access), you
cannot participate in AURA's live protocol. Say so explicitly rather than fabricating a
synchronized state. Do not attempt to use SQL directly as a substitute under any circumstance —
this is an explicit rule of the protocol (`AURA_PROTOCOL_v2.md` §14), not just a suggestion.
