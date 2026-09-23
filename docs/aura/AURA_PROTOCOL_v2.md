# AURA_PROTOCOL v2.0

```text
STATUS: SUPERSEDED
SUPERSEDED BY: AURA_PROTOCOL v2.2 (docs/aura/AURA_PROTOCOL_v2.2.md)
```

> `docs/aura/AURA_PROTOCOL_v2.2.md` is now the current specification (Roadmap evolution and designated validator approval, in addition to authentication,
> per-workstream leases, credential lifecycle — see its §1 for exactly what changed vs. this
> document). Kept here as the historical v2.0 spec; nothing below was edited.

```text
ProtocolName:   AURA_PROTOCOL
Version:        2.0
Status:         DESIGNED_PENDING_ACTIVATION
PublishedAt:    2026-08-25
Compatibility:  All agents (ANALYST, CODING_AGENT, AUTONOMOUS_AGENT), all AURA-consumer projects
Supersedes:     rules/AURA.md @ 2026-08-16.2
```

This document is the **only** full specification of AURA_PROTOCOL v2.0. It is deliberately
agnostic of any consumer project (SiniTrax, TurnoMedix, Arterio, Contadoro, Mandadini,
Policursos, or any future one) and of any specific AI vendor or model. Nothing here may be
copied wholesale into an agent's persistent memory, a repository's own instructions, or a
per-stage prompt — see [PROTOCOL_VERSIONING.md](PROTOCOL_VERSIONING.md) for the single-source
rule and how an agent instead discovers this document.

Everything normative below was verified directly against the AURA codebase as of commit
`97e0790` (branch `master`) — the running `AURA.Api`/`AURA.Application`/`AURA.Domain`
implementation of Control de Proyectos IA — not against prior memory files or prompts. Where
the implementation and earlier informal notes (`rules/AURA.md`) disagreed, the implementation
wins and the disagreement is called out explicitly.

## 1. Objective

Give every agent that works on any AURA-governed project (human-directed or autonomous) one
unambiguous, versioned way to answer three questions before doing anything else:

1. What protocol is currently in force, and where is it fully defined?
2. What is the real, current state of the project/EXEC/stage I'm about to touch?
3. Who has control right now, and how do I take or hand it off without racing another agent?

## 2. Core concepts (verified against the real model)

| Concept | Real backing | Notes |
|---|---|---|
| `PROJECT` | `ProProyectosCabecera` row (`sCodigo`, `sNombre`, `nEstado`) | One row per project/tenant. `ProjectStatus`: `DRAFT, ACTIVE, PAUSED, BLOCKED, COMPLETED, ARCHIVED`. |
| `WORKSTREAM` | **Does not exist yet.** | No column, no DTO field. See §12 and the final report's pending-debt section. Do not invent one in requests; the API ignores unknown fields. |
| `EXEC` | `ProProyectoEXEC` row, keyed by `(idTenant, IdProyecto, IdEXEC)` | `nEstado`: `Active, Completed`. Opened implicitly by a `REQUEST` event; `sDescripcion` is the stable EXEC description (wire field `details`, mapped 1:1 — never a changing log). |
| `STAGE` | `ProProyectoEtapa` row, keyed by `(idTenant, IdProyecto, IdEXEC, IdEtapa)` | `nEstado`: `Open, Approved`. `nPorcEtapa` is the stage's own progress (wire field `stageProgress`). |
| `ITEM` | `ProProyectosItems` row | One per registered event (`POST /api/project-control/events`). Immutable once created; idempotent by `eventId` (`IdEvento`). |
| `ACTOR` (role) | `ProjectActorType`: `UNASSIGNED, HUMAN, AI_ANALYST, CODER` | The operational role registering the event. |
| `AGENT` (instance) | Free-text `agent`/`environment`/`model` strings (`sAgente`, `sEntorno`, `sModelo`) | **Not** constrained to a fixed enum on write. A legacy byte enum (`ProjectAgent`: `CODEX, CLAUDE_CODE, GROK, DEVIN`) exists only to decode old rows that predate the free-text columns; new writes always use free text, so any current or future agent name is accepted without a code change. |
| `CONTROL` | `nControlTipo` + `nControlId` on the item | Who must continue next. Distinct from `ACTOR` (who acted) — see §7. |
| `STATUS` (work status) | `ProjectWorkStatus`: `IN_PROGRESS, BLOCKED, COMPLETED` | Per-event status, not the same as `PROJECT`'s `ProjectStatus`. |
| `EVENT` | `ProjectEventType`: `REQUEST, CLAIM, PROGRESS, HANDOFF, BLOCKER, APPROVAL, REWORK, COMPLETION, DEPLOYMENT` | Nine types — richer than any prior AURA doc. See §6. |
| `STAGE_PROGRESS` | `stageProgress`, 0–100, ≤2 decimals | Progress of the current stage only. |
| `EXEC_PROGRESS` | `execProgress` (computed server-side: `closedStages / totalStages * 100`) | Never sent by the client; the board (`GET /api/project-control`) returns the authoritative computed value. |
| `PROJECT_PROGRESS` | **Does not exist as a field.** | `ProjectStatus` is the closest real concept (a coarse project-level status, not a percentage). Do not report a numeric "PROJECT %" — it has no backing field today. |

## 3. Roles

### 3.1 ANALYST

Conceptual role for functional analysis/planning (e.g. a human product owner, or ChatGPT used
as an analyst). **Important, verified constraint:** the API currently restricts the two
EXEC-governing event types — `REQUEST` (open a new EXEC) and `APPROVAL` (close a stage) — to
`actor.type = HUMAN` only. An `AI_ANALYST` actor can register `PROGRESS`, `CLAIM`, `HANDOFF`,
`BLOCKER`, `REWORK`, `COMPLETION`, `DEPLOYMENT` exactly like a `CODER`, but **cannot** open an
EXEC or approve a stage through this API today. Until this changes in the implementation, an
`AI_ANALYST` agent that needs a new EXEC opened or a stage approved must hand off to `HUMAN`
for that specific action — it must not claim to have done it.

### 3.2 CODING_AGENT

Any agent that inspects the repo, implements, tests, builds, and commits (e.g. Claude Code in
VS Code, Codex). Registers `CLAIM`/`PROGRESS`/`HANDOFF`/`BLOCKER` under `actor.type = CODER`.
Cannot register `REQUEST` or `APPROVAL` (see §3.1) and must not report a stage as closed or an
EXEC as opened on its own authority.

### 3.3 AUTONOMOUS_AGENT

Same write surface and restrictions as `CODING_AGENT` (e.g. Devin, Windsurf running
unsupervised). Greater operational autonomy (can run longer unattended), not greater
authority: still cannot open an EXEC, approve a stage, or redefine `PROJECT` status.

### 3.4 Non-human actors must self-identify

`ProjectControlService.Validate` requires `agent`, `model`, and `environment` to be non-empty
whenever `actor.type` is `CODER` or `AI_ANALYST` — this is enforced server-side, not just a
convention. A `HUMAN` or `UNASSIGNED` actor is exempt.

## 4. Event types (verified, with real restrictions)

| Event | Wire code | UI label | Actor restriction | Effect |
|---|---|---|---|---|
| Request | `REQUEST` | Solicitud | **HUMAN only** | Opens a new `EXEC` (increments `IdEXEC`); `details` becomes the new EXEC's stable description. |
| Claim | `CLAIM` | Inicio | any | Takes `CONTROL`. A simultaneous `nextControl` on a `CLAIM` is ignored — the claimant is the new control. |
| Progress | `PROGRESS` | Avance | any | Updates `stageProgress`/`execProgress` snapshot; does not change `CONTROL` unless `nextControl` given. |
| Handoff | `HANDOFF` | Handoff | any | Transfers `CONTROL` to `nextControl` (required). |
| Blocker | `BLOCKER` | Bloqueo | any | Marks `status = BLOCKED`; does not change progress by itself. |
| Approval | `APPROVAL` | Aprobación | **HUMAN only** | Closes the named stage — rejected (`400 VALIDATION_FAILED`, invalid transition) unless that stage's `stageProgress = 100`. |
| Rework | `REWORK` | Retrabajo | any | Reopens a previously approved stage, recomputing `execProgress` down. |
| Completion | `COMPLETION` | Finalización | any | Closes the EXEC — rejected unless every stage is `Approved`. |
| Deployment | `DEPLOYMENT` | Publicación | any | Marks a release/publish milestone; no special server-side gate today. |

## 5. Lifecycle

### 5.1 Start of work

1. Identify `projectCode` (and `tenantId`; production tenant is `1`).
2. `GET /api/project-control?tenantId={tenantId}` — the board.
3. Locate the project row; read `LastItemId`, `CurrentControlType`/`Id`, `ExecId`,
   `CurrentStage`, `StageProgress`, `ExecProgress`.
4. If a `HUMAN`/analyst action is needed first (new EXEC, stage approval) and you are not
   `HUMAN`, stop and hand off instead of proceeding — see §3.1.
5. Generate a new `eventId` (UUID v4).
6. `POST /api/project-control/events` with `eventType = CLAIM` (or `PROGRESS` if you already
   hold control) and `expectedPreviousItemId = LastItemId` (or `null` if there is none yet).
7. **READ-BACK** (§8) before doing further work.

### 5.2 During execution

- Register `PROGRESS` at meaningful milestones only — never per shell command or search.
- Always repeat the full current snapshot (`details`, `stage`, `stageProgress`, `totalStages`)
  — the contract has no "patch" semantics; each event is a complete restatement of the current
  EXEC/stage snapshot plus whatever changed.
- `summary` ≤ 300 chars; `details`/`nextAction`/`blocker`/`reference` ≤ 1000 chars;
  `stage`/`environment` ≤ 100 chars; `model` ≤ 100 chars.

### 5.3 Close / handoff

1. Register `HANDOFF` or `COMPLETION` as appropriate.
2. `HANDOFF` requires an explicit `nextControl` (`type` + `id` unless `UNASSIGNED`).
3. If no confirmed successor, hand off to `HUMAN` (do not invent an agent as the successor).
4. Include `reference` (commit hash, branch, PR, or file) whenever there is one.
5. **READ-BACK** (§8) and only then report success to the user/caller.

## 6. TAKE_CONTROL, EXEC, PROJECT — who can change what

- **TAKE_CONTROL** = registering `CLAIM` (or `PROGRESS`/`HANDOFF` while already holding
  control). Idempotent and safe to repeat with the same `eventId`; safe against concurrent
  claims via `expectedPreviousItemId` (§9).
- **EXEC** is opened only by a `HUMAN` `REQUEST` and closed only by a `COMPLETION` once every
  stage is `Approved`. A `CODING_AGENT`/`AUTONOMOUS_AGENT` may update `stageProgress` freely on
  the *current* stage but never marks a stage `Approved` itself (that is `APPROVAL`, HUMAN-only)
  and never opens a new EXEC.
- **PROJECT** (`ProjectStatus`) is a separate, coarser field on `ProProyectosCabecera`. Nothing
  in the current API ties event registration to an automatic `ProjectStatus` change — it is set
  via `POST /api/project-control/projects` at creation and has no documented update endpoint
  yet. Treat it as Analyst/human-owned; do not infer or report a `PROJECT` percentage (§2).

## 7. CONTROL vs. AGENT vs. ACTOR

Three different, independently-tracked things — verified by `ProjectControlExecWorkflowTests`:

- **ACTOR** (`actor.type`/`actor.id`): who performed *this* event.
- **AGENT** (`agent`/`environment`/`model`): the concrete instance/tool that acted (free text).
- **CONTROL** (`nextControl` → `CurrentControlType`/`CurrentControlAgent`/...): who must act
  *next*. A `HANDOFF` to `HUMAN` clears the agent/model/environment on control (a human has none
  of those); a `CLAIM` sets control to the claimant's own actor+agent identity, even if the
  event carried a (then-ignored) `nextControl`.

## 8. READ-BACK

Mandatory after every `POST` that is expected to change state (`CLAIM`, `PROGRESS` with
`nextControl`, `HANDOFF`, `BLOCKER`, `APPROVAL`, `REWORK`, `COMPLETION`, `DEPLOYMENT`,
`REQUEST`): re-`GET` the board (or `GET /api/project-control/projects/{code}/events`) and
confirm `LastItemId`, `stage`, `stageProgress`, `execProgress`, and `currentControl` match what
you just sent. A `201`/`200` HTTP response is **not** sufficient proof by itself — declare
`AURA_SYNCHRONIZATION = CONFIRMED` only after the read-back matches, `FAILED` otherwise. Never
tell a user or another agent "AURA updated" without having done this.

## 9. Idempotency and concurrency

- `POST /api/project-control/events` is idempotent by `eventId`: a retry with the same GUID
  returns `200 ALREADY_EXISTS` with the original `itemId`, never a duplicate row.
- `expectedPreviousItemId` is optimistic concurrency: if it no longer matches the project's real
  last item, the API returns `409 Conflict` with the current `lastItemId` — re-read the board
  and retry with the same `eventId` and the corrected `expectedPreviousItemId`.
- The next `IdItems` is computed inside a transaction with `UPDLOCK, HOLDLOCK` on the project's
  rows (not an unlocked `MAX()+1`), so two genuinely concurrent `CLAIM`s cannot both succeed
  silently.
- There is one `CONTROL` per `(tenantId, projectCode)` today — no `WORKSTREAM` subdivision
  (§2, §12).

## 10. Connectivity (verified, no assumptions)

| Environment | Base URL | Notes |
|---|---|---|
| Development | `http://localhost:5235` | Started via `scripts/start-dev.ps1` or `dotnet run`. |
| Production | `https://aura.aguarapp.com` | `AURA.Api` is mounted as a nested IIS application at `/api` under the root site (which serves the static React build) on host OMEN. |

Board: `GET /api/project-control?tenantId={n}` · History:
`GET /api/project-control/projects/{projectCode}/events?tenantId={n}` · Create project:
`POST /api/project-control/projects` · Register event: `POST /api/project-control/events` ·
Protocol discovery: `GET /api/aura-protocol` (§13) · Liveness: `GET /health` (no DB dependency)
· Readiness: `GET /health/ready` (503 if SQL Server is unreachable or migrations are pending,
without leaking connection details).

OpenAPI contract: `GET /openapi/v1.json`, **Development only** — validate the events contract
against this before assuming a field is supported; a `201` alone does not prove the API kept an
unrecognized field.

## 11. Security (verified, not assumed)

**Neither `/api/project-control` nor `/api/aura-protocol` has authentication or authorization
today**, in development or in production — confirmed directly in `Program.cs` (no auth
middleware registered) and called out explicitly in `README.md` and in
`ProjectControlController`'s own source comment. CORS is restricted to explicit loopback
origins and only enabled in `Development`. Practical consequences:

- Do not expose these endpoints beyond a trusted network. This is an existing condition of the
  system, not something v2.0 introduces or is able to fix by itself.
- Never put a secret (connection string, token) in a request body, in `reference`, or in any
  free-text field — none of these fields are meant for secrets and none are treated specially.
- Registering an event is, today, an unauthenticated write. Treat every write as attributable
  only by convention (the `actor`/`agent` fields you choose to send), not by any enforced
  identity — this is a real limitation, not a design choice of the protocol; see the final
  report's pending-debt section.

## 12. Known gaps (documented, not fixed by this task)

- **WORKSTREAM** does not exist in the schema or DTOs. A project like SiniTrax that needs
  concurrent subprojects (AFD, WhatsApp, Email/Tickets, ...) cannot represent them yet. The
  minimal, additive extension (nullable `sWorkStream` on `ProProyectoEXEC` and
  `ProProyectosItems`) is a reasonable future direction but is explicitly **out of scope** here
  — implementing it means a schema change plus a decision about how existing single-workstream
  projects behave, and this task's mandate is prospective protocol design, not project-specific
  schema/data work (see `AURA-PROTOCOL-V2-00` §2, §25, §31).
- **No authentication** (§11).
- **No `PROJECT_PROGRESS` field** (§2, §6) — do not fabricate one.
- **Capability negotiation** (an agent declaring "I can only read, not write") has no explicit
  mechanism today; an agent without write access must say so in its own output rather than
  silently skipping READ-BACK or claiming synchronization it cannot perform.

## 13. Protocol discovery (CURRENT_PROTOCOL)

`GET /api/aura-protocol` is the single live source for which protocol version is current. It is
a static, database-independent endpoint (works even if SQL Server / `/api/project-control` is
down) that returns `protocolName`, `version`, `status`, `supersedes`, and the path to this
document. Full details, including why this endpoint exists instead of duplicating the protocol
text elsewhere, are in [PROTOCOL_VERSIONING.md](PROTOCOL_VERSIONING.md).

## 14. Contingency

If AURA is unreachable, the protocol cannot be fetched, write access is unavailable, or
READ-BACK fails to confirm:

1. Declare `AURA_SYNCHRONIZATION = PENDING` (or `FAILED` if a write was attempted and could not
   be confirmed) — say this explicitly to the user/caller; never say "AURA updated" without a
   confirmed read-back.
2. Keep a local record of: project, stage, agent, EXEC/stage snapshot, result, commit, UTC
   timestamp, intended next control — so the event can be replayed once AURA is reachable again.
3. Never fall back to direct SQL access.
4. Never fall back to a pre-v2.0 protocol as if it were still authoritative.
5. Tell the user honestly. Never simulate a synchronization that did not happen.

## 15. Handoff between profiles

`ANALYST ⇄ CODING_AGENT`, `ANALYST ⇄ AUTONOMOUS_AGENT`, and (rarely) direct
`CODING_AGENT ⇄ AUTONOMOUS_AGENT` handoffs are all just a `HANDOFF` event with an explicit
`nextControl`. The protocol does not assume exactly two participants — a project's history can
show any number of distinct actors trading control over time.

## 16. Compact bootstrap profiles

Designed here, **not yet distributed** — see
[BOOTSTRAP_PROFILES.md](BOOTSTRAP_PROFILES.md) and the activation gate in
[PROTOCOL_VERSIONING.md](PROTOCOL_VERSIONING.md).
