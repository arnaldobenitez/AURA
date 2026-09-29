# AURA_PROTOCOL v2.2

```text
ProtocolName:   AURA_PROTOCOL
Version:        2.2
Status:         IMPLEMENTED_PENDING_PRODUCTION_VERIFICATION
PublishedAt:    2026-09-23
Compatibility:  All agents (ANALYST, CODING_AGENT, AUTONOMOUS_AGENT), all AURA-consumer projects
Supersedes:     docs/aura/AURA_PROTOCOL_v2.1.md @ 2.1
```

This remains the normative specification for `GOVERNED_EXECUTION`. It carries forward the authentication, tenant isolation, leases, optimistic concurrency, idempotency, and Project Memory separation established by v2.1, and adds a persistent ordered Roadmap with per-stage progress and designated AI_ANALYST approval. v2.1 remains available for audit but is no longer current. `AURA Agent Registration Protocol v2.3` adds the separate `REGISTRATION_ONLY` channel without replacing this document.

## 1. Changes in v2.2

| Area | v2.2 behavior |
|---|---|
| Roadmap | Ordered stage definitions and criteria are persisted per project, active EXEC, and workStream. |
| Stage progress | `stageCode` identifies a planned Roadmap stage; `stageProgress` remains 0–100. |
| Approval | A planned stage is formally approved by its assigned, active `AI_ANALYST` credential, not by its CODER. Legacy stages without a Roadmap code retain the HUMAN approval rule. |
| Outcome | Roadmap approval requires `stageOutcome = PASS` or `PASS_WITH_WARNINGS`; both close the milestone, warnings remain visible. |
| Discovery | `GET /api/aura-protocol` now reports v2.3; `governedSourceDocument` continues to point here for `GOVERNED_EXECUTION` and exposes the Roadmap endpoints. |

## 2. Credentials and tokens

`AgentCredential` registers a tenant-scoped identity (`HUMAN`, `AI_ANALYST`, or `CODER`; v2.3 adds technical `INGESTION` credentials outside this governed contract, never `UNASSIGNED`), agent name, optional `LinkedActorId`, and a salted PBKDF2-SHA256 secret hash. New `INGESTION` credentials are additionally project-scoped through `AgentCredentialProject`; legacy tenant-wide rows are explicitly marked during transition. Project scope is enforced by `REGISTRATION_ONLY`, not by governed execution. Plaintext credential secrets are returned only at creation/rotation and must be stored securely.

- `POST /api/admin/agent-credentials` creates a credential.
- `POST /api/admin/agent-credentials/{id}/rotate?tenantId=n` rotates it.
- `POST /api/admin/agent-credentials/{id}/revoke?tenantId=n` revokes it.
- `GET /api/admin/agent-credentials?tenantId=n` returns metadata only.

Credential administration uses the human portal JWT (`HUMAN`, signed source `PORTAL_LOGIN`) plus the tenant-specific allowlist `Admin:TenantAdminUserIds:<tenantId>`. `X-Admin-Api-Key` remains only for backend compatibility/break-glass calls and is never sent by the browser. Secrets are returned only once during create/rotate.

`POST /api/auth/token` exchanges `{ tenantId, credentialId, secret }` for a short-lived Bearer JWT. `POST /api/auth/login` exchanges a human email/password for a human JWT. Never put a password, credential secret, JWT, connection string, or other secret in an event or Roadmap field.

## 3. Authentication and authorization

All ProjectControl reads and writes require a Bearer JWT. The server derives actor type, tenant, credential ID and any linked actor ID from the validated token; body claims cannot override them. A tenant mismatch is forbidden. If the credential pins a `LinkedActorId`, the body actor ID must match. `INGESTION` credentials introduced by v2.3 are excluded from governed writes and project administration.

- `REQUEST` and Roadmap plan administration require an authenticated `HUMAN` credential.
- A legacy `APPROVAL` without a Roadmap `stageCode` requires `HUMAN`.
- A Roadmap `APPROVAL` requires `AI_ANALYST` and the credential assigned to that exact stage. A `CODER`, a different analyst credential, or the credential that most recently recorded the stage at 100% cannot approve it.
- `CLAIM`, `PROGRESS`, `HANDOFF`, `BLOCKER`, `REWORK`, `COMPLETION` and `DEPLOYMENT` follow their existing authenticated-role and lease rules.

The human administering a plan is responsible for assigning an active AI_ANALYST credential. The assigned reviewer cannot be supplied or changed by a CODER event.

## 4. Project, EXEC and workStream scope

An EXEC is a project work unit; it is not the project itself. Roadmap stages are scoped by `(tenantId, projectCode, execId, workStream)`. A null workStream is the default whole-project chain. Distinct workStreams have independent EXECs, Roadmaps, stages, event chains and leases. Physical item and EXEC IDs remain project-wide.

## 5. Leases and concurrency

The lease covers exclusive work: `CLAIM`, `PROGRESS`, `HANDOFF`, `BLOCKER` and `DEPLOYMENT`. `CLAIM` obtains/renews a lease. The other exclusive-work events must include the current `leaseId` and `expectedLeaseVersion`; successful writes advance the version. A stale, expired or differently-held lease returns `409 LEASE_CONFLICT`. `REQUEST`, `APPROVAL` and `REWORK` are governance operations and do not require the work lease. `COMPLETION` releases it.

Every event write also uses `expectedPreviousItemId`; on `409 CONFLICT`, re-read the board and retry with the new actual item ID. Keep the same `eventId` for an idempotent retry. Read back after every accepted write.

## 6. Roadmap API

| Purpose | Method + path | Authority |
|---|---|---|
| Protocol discovery | `GET /api/aura-protocol` | None |
| Read ordered Roadmap | `GET /api/project-control/projects/{projectCode}/roadmap?tenantId={n}[&workStream=...]` | Bearer JWT |
| Define/update Roadmap | `PUT /api/project-control/projects/{projectCode}/roadmap` | HUMAN Bearer JWT |
| Register stage progress/approval | `POST /api/project-control/events` | Bearer JWT |

The `GET` returns the selected active EXEC, ordered stages, planned total, closed count, computed global percentage, per-stage progress/status, acceptance criteria, assigned reviewer, and the latest approval result/evidence where available. If the project exists but has no EXEC/Roadmap yet, it returns an empty stage list; unknown projects return `404`.

The plan write body is:

```json
{
  "tenantId": 1,
  "workStream": null,
  "eventId": "<new UUID>",
  "stages": [
    {
      "stageCode": "SMT-00.1",
      "description": "Security baseline",
      "order": 1,
      "acceptanceCriteria": "Build and security tests pass",
      "validatorCredentialId": 42
    }
  ]
}
```

Plan rules:

- An active EXEC must already exist. The plan applies to that EXEC and workStream.
- Plan stages must be 1–200, have unique non-empty stage codes (≤ 50 chars), consecutive unique orders starting at 1, descriptions (≤ 100 chars), and an active AI_ANALYST validator credential from the same tenant.
- Plan updates are idempotent by `eventId` and audited as Roadmap revisions. Do not reuse an event ID for a different plan.
- Existing stages cannot be removed. The submitted plan must include them. A legacy stage with no Roadmap code may be mapped once (code, criteria, validator and order) while preserving its existing progress/status. After it is mapped, its code, description, order and acceptance criteria are locked once progress exists or it is approved; a HUMAN may reassign its validator.
- New stages may be added. The plan total updates the EXEC denominator. This is a controlled plan change and can therefore change global completion percentage; record why in the release/task context.
- The schema migrator is additive and explicit. A deployment operator must run the approved schema-update procedure; API startup does not apply it automatically.

## 7. Stage progress and approval contract

A normal work event identifies a planned stage with `stageCode` and may include its display `stage` and `stageProgress`. The progress is a number from 0 to 100 with up to two decimals and must be based on completed, verifiable criteria. The stage description/code must match the active Roadmap; unknown codes are rejected.

Example progress fields:

```json
{
  "eventType": "PROGRESS",
  "stageCode": "SMT-00.1",
  "stage": "Security baseline",
  "stageProgress": 65.00,
  "summary": "Completed password-hash migration tests",
  "reference": "commit SHA, test result, or relevant file",
  "leaseId": "<current lease>",
  "expectedLeaseVersion": 3
}
```

Roadmap approval uses the ordinary event endpoint and item-chain concurrency:

```json
{
  "eventType": "APPROVAL",
  "stageCode": "SMT-00.1",
  "stage": "Security baseline",
  "stageOutcome": "PASS_WITH_WARNINGS",
  "summary": "Validation result and remaining non-blocking warnings",
  "reference": "build/test results, commit, or evidence location"
}
```

`stageOutcome` is required for a Roadmap approval and accepts only `PASS` or `PASS_WITH_WARNINGS`. The stage must be at 100%; its assigned AI_ANALYST credential must be the authenticated caller; and that credential must differ from the credential that recorded the technical completion. The accepted approval event, actor/model, outcome, summary, reference and timestamp are persisted for audit. Legacy stages without a Roadmap code continue to use the prior HUMAN approval path.

`REWORK` reopens the stage and resets its technical progress to zero. A new coder progress update and a new designated analyst approval are required before the stage can close again.

## 8. Global progress semantics

The global EXEC percentage is based only on closed planned milestones:

```text
execProgress = (PASS stages + PASS_WITH_WARNINGS stages) / total planned stages * 100
```

Round to two decimals. `PASS_WITH_WARNINGS` closes a milestone and counts toward the global percentage, but the warning outcome and evidence remain visible. A stage's partial technical percentage is displayed on that stage and is not added to the global percentage. A blocker does not change either percentage without new evidence. Completion requires every planned stage to have a terminal PASS outcome (including PASS_WITH_WARNINGS) and the existing EXEC completion rules to pass.

## 9. Event codes and response behavior

Supported event types remain `CLAIM`, `PROGRESS`, `HANDOFF`, `BLOCKER`, `APPROVAL`, `REWORK`, `REQUEST`, `COMPLETION`, and `DEPLOYMENT`. Statuses remain `IN_PROGRESS`, `BLOCKED`, and `COMPLETED`.

- `201 CREATED`: accepted new event.
- `200 ALREADY_EXISTS`: `eventId` was already committed; treat as success and read back.
- `400 VALIDATION_FAILED`: invalid plan/event/outcome/transition; response contains errors.
- `403 Forbidden`: authenticated identity is not authorized for the operation/tenant.
- `404`: unknown project/roadmap resource.
- `409 CONFLICT`: stale item-chain ID; re-read and retry using the same event ID.
- `409 LEASE_CONFLICT`: lease is stale, expired or held by another credential.

A successful POST by itself is not enough. Verify the new item and status with GET/read-back before reporting synchronization.

## 10. AURA Project Memory

Project Memory remains a complementary local documentation layer. When `.aura/project.json` exists, resolve the nearest valid project root, read the Core, and follow `AURA_AGENT_CONTRACT`; it does not grant operational authority or substitute for the API. Local documentation changes do not prove that an event or Roadmap write persisted. Never use SQL as a fallback for API writes.

## 11. Production activation gate

This protocol remains `IMPLEMENTED_PENDING_PRODUCTION_VERIFICATION` until a real deployment, controlled production smoke test, and Roadmap/event read-back pass. Do not treat it as `ACTIVE`, and do not distribute bootstraps as authoritative before the Analyst records that verification.
