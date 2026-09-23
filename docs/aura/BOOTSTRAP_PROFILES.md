# AURA Bootstrap Profiles (v2.2)

```text
STATUS: DESIGNED — NOT YET DISTRIBUTED
```

These three profiles are the only thing a repository or an agent's own instructions should ever
receive — never the full text of `AURA_PROTOCOL_v2.2.md`. Per `AURA-PROTOCOL-V2-00` §24/§34 and
`AURA-PROTOCOL-V2-01` (which keeps this the same, explicitly: "NO distribuir bootstraps
todavía"), they are designed here for review but must **not** be pasted into any live agent
memory, `CLAUDE.md`, ChatGPT custom instructions, Windsurf/Devin configuration, or any consumer
repository until an Analyst has reviewed both reports and explicitly authorized distribution.
Distributing them is a separate, future action — do not perform it as a side effect of reading
this file. **Secrets (a credential's secret, the `Admin:ApiKey`, `Jwt:SigningKey`) never form
part of a bootstrap under any circumstance** — a bootstrap says how to obtain a token, never
carries one.

Each profile is intentionally small: identity + role + how to authenticate + where to fetch the
real protocol + what to do if AURA is unreachable. Nothing else.

## Common Project Memory discovery

All profiles use the same generic discovery rule and do not duplicate `templates/AURA.md`:

```text
If the working project contains .aura/project.json:
  resolve PROJECT_ROOT using the nearest valid manifest;
  read AURA.md, the manifest, and the Project Memory Core;
  load optional modules only when the task requires them.
Project Memory is local documentation; it does not grant operational authority or replace AURA read-back.
```

This rule applies equally to `ANALYST`, `CODING_AGENT`, `AUTONOMOUS_AGENT`, and future roles. A profile may continue locally when the API is unavailable, but must record operational synchronization as pending and must not fabricate events, leases, or read-back.

## AURA_BOOTSTRAP_ANALYST_v2

```text
PROTOCOL: AURA_PROTOCOL (ask GET /api/aura-protocol for the current version — do not hardcode a number)
ROLE: HUMAN ANALYST / ROADMAP ADMINISTRATOR
CREDENTIAL: your own AgentCredential (actorType=HUMAN), provisioned once by an admin. Never share it.
ON START: POST /api/auth/token with your credentialId+secret -> Bearer token.
          GET /api/aura-protocol, then read the document it names before acting.
WRITE SCOPE: REQUEST to open an EXEC; PUT the ordered Roadmap and assign an active AI_ANALYST
             credential to each stage. You may approve legacy stages without a Roadmap code;
             a planned Roadmap stage must be approved by its designated AI_ANALYST credential.
SYNC: Before any significant task: GET the board. After any write: READ-BACK before reporting success.
IF TOKEN EXPIRED: request a new one (POST /api/auth/token again) — no separate refresh flow.
IF AURA UNAVAILABLE: declare AURA_SYNCHRONIZATION = PENDING, keep a local record, do not use SQL
             directly, do not fall back to any superseded protocol as authoritative.
```

## AURA_BOOTSTRAP_ROADMAP_VALIDATOR_v2.2

```text
PROTOCOL: AURA_PROTOCOL (ask GET /api/aura-protocol for the current version — do not hardcode a number)
ROLE: DESIGNATED ROADMAP VALIDATOR
CREDENTIAL: your own AgentCredential (actorType=AI_ANALYST), assigned by a HUMAN to each stage. Never share it.
ON START: POST /api/auth/token with your credentialId+secret -> Bearer token.
          GET /api/aura-protocol, read its sourceDocument, then GET the board and Roadmap.
WRITE SCOPE: Review only stages assigned to this credential. Verify acceptance criteria and
             evidence. If complete, send APPROVAL with stageCode and stageOutcome PASS or
             PASS_WITH_WARNINGS, summary, and reference. You cannot edit/assign the plan or
             approve a stage completed by this same credential.
SYNC: READ-BACK the Roadmap after every approval before reporting success.
IF AURA UNAVAILABLE: report synchronization as PENDING; never use SQL to update Roadmap.
```

## AURA_BOOTSTRAP_CODING_AGENT_v2

```text
PROTOCOL: AURA_PROTOCOL (ask GET /api/aura-protocol for the current version — do not hardcode a number)
ROLE: CODING_AGENT
CREDENTIAL: your own AgentCredential (actorType=CODER), provisioned once by an admin. Never share it.
IDENTITY: agent/model/environment are free text and required for this actor type.
ON START: POST /api/auth/token with your credentialId+secret -> Bearer token.
          GET /api/aura-protocol, read the document it names, then GET the board and Roadmap.
          Use the planned stageCode and acceptance criteria; note LastItemId and CurrentControl.
WRITE SCOPE: CLAIM to take control; PROGRESS verified stageProgress with stageCode and the
             latest lease; HANDOFF or BLOCKER to close out. Never send formal APPROVAL — hand
             the stage to its designated AI_ANALYST when it reaches 100%.
CONCURRENCY: a second CLAIM by a different credential on the same (project, workStream) fails
             with 409 LEASE_CONFLICT — that's correct, not a bug; re-check who holds it.
SYNC: READ-BACK after every write before telling anyone it succeeded.
IF TOKEN EXPIRED: request a new one (POST /api/auth/token again).
IF AURA UNAVAILABLE: declare AURA_SYNCHRONIZATION = PENDING, keep a local record, do not use SQL
             directly, do not fall back to any superseded protocol as authoritative.
```

## AURA_BOOTSTRAP_AUTONOMOUS_AGENT_v2

```text
PROTOCOL: AURA_PROTOCOL (ask GET /api/aura-protocol for the current version — do not hardcode a number)
ROLE: AUTONOMOUS_AGENT
CREDENTIAL: your own AgentCredential (actorType=CODER, same as CODING_AGENT). Never share it.
IDENTITY: same identity rules as CODING_AGENT; may run longer unattended, this does not grant
          extra authority over EXEC/PROJECT/stage approval.
ON START / WRITE SCOPE / CONCURRENCY: same as CODING_AGENT.
ADDITIONAL CONSTRAINT: never redefine project scope, start a project unprompted, or take over a
          task another actor already controls without an explicit HANDOFF or a resolved
          409 (CONFLICT or LEASE_CONFLICT).
SYNC / UNAVAILABILITY / TOKEN EXPIRY: identical to CODING_AGENT.
```

## Activation checklist (for whoever authorizes distribution later)

- [ ] `AuraProtocolMetadataProvider.Current.Status` flipped from
      `IMPLEMENTED_PENDING_PRODUCTION_VERIFICATION` to `ACTIVE` (see `PROTOCOL_VERSIONING.md` §3)
      — only after production deployment + a controlled production smoke test + read-back all
      pass on `https://aura.aguarapp.com` (see `AURA-PROTOCOL-V2-01`'s final report).
- [ ] `rules/AURA.md`'s obsolescence header updated to reflect actual activation, not just design.
- [ ] Real `AgentCredential` rows created for each agent that will receive a bootstrap (not the
      shared test credentials used by the automated test suite).
- [ ] `Jwt:SigningKey` and `Admin:ApiKey` configured in production exactly like
      `ConnectionStrings:DefaultConnection` already is (hand-edited into the deployed
      `appsettings.json` on OMEN) — never committed, never in a bootstrap.
- [ ] Each profile above copied into exactly the small bootstrap location the Analyst chooses
      per agent/environment (never the full `AURA_PROTOCOL_v2.2.md`).
- [ ] No consumer project's own data (SiniTrax, TurnoMedix, Arterio, ...) touched as part of
      this step — reconciling any of them stays a separate, explicitly authorized task.
