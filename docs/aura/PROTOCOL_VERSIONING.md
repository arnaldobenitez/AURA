# AURA Protocol — Versioning, Discovery, and Deprecation

## 1. Single-source principle

The full text of the CURRENT version of AURA_PROTOCOL lives in exactly one place:
**`docs/aura/AURA_PROTOCOL_v2.1.md`** today (plus its two companions, `AGENT_INTEGRATION.md` and
this file). Superseded versions (`AURA_PROTOCOL_v2.md`) are kept for audit trail with an
obsolescence header (§6) — never treated as current. It must never be copied in full into:

- an agent's persistent/cross-session memory,
- a consumer repository's own instructions or CLAUDE.md-equivalent,
- a per-stage or per-task prompt.

Any of those may hold a **bootstrap** — a small pointer, not the protocol itself (§4).

## 2. How an agent discovers CURRENT_PROTOCOL

Two things must agree, and both are checked, not assumed:

1. **Live source**: `GET /api/aura-protocol` (implemented in
   `src/AURA.Api/Controllers/AuraProtocolController.cs`, backed by
   `AURA.Application.AuraProtocol.AuraProtocolMetadataProvider`). Static, database-independent —
   it answers even when SQL Server (and therefore `/api/project-control`) is unreachable, so an
   agent can always at least confirm which protocol version is current.
2. **Document source**: the file named in that response's `sourceDocument` field
   (`docs/aura/AURA_PROTOCOL_v2.1.md` today).

There is deliberately **no third, independent source** (no separate config file, no DB table) —
the task that designed v2.0 explicitly warned against multiple sources of protocol-version truth
that could drift apart. If the API's `version`/`status` and the document's own header ever
disagree, that is a bug: fix both together in the same change. Never resolve the mismatch by
trusting one and ignoring the other.

## 3. Status values

| `status` | Meaning |
|---|---|
| `DESIGNED_PENDING_ACTIVATION` | Specification complete, no code yet (v2.0's original state). |
| `IMPLEMENTED_PENDING_PRODUCTION_VERIFICATION` | Implemented and locally verified (tests + local smoke), but not yet deployed/verified in production and not yet the operational fallback for all agents. **This is the value today** (v2.1: authentication + lease implemented, `https://aura.aguarapp.com` deployment/verification still pending — see `AURA-PROTOCOL-V2-01`'s final report §S/§T). |
| `ACTIVE` | Activated: agents should treat this version as authoritative and `rules/AURA.md`'s pre-v2.x content as historical only. Not yet reached. |
| `DEPRECATED` | A newer version now supersedes this one; kept readable for audit/history. |

Changing the status is a one-line code change in `AuraProtocolMetadataProvider.Current`
(`src/AURA.Application/AuraProtocol/AuraProtocolMetadataDto.cs`) plus a matching update to this
document's own status line — intentionally lightweight, since flipping it is a real operational
decision (the Analyst's activation), not a schema or data migration.

## 4. Bootstraps (forward compatibility mechanism)

A bootstrap is the minimal thing a consumer repository or an agent's own instructions may
contain: which protocol is current, which role the agent plays, how to fetch the real protocol,
and what to do if AURA is unreachable. See [BOOTSTRAP_PROFILES.md](BOOTSTRAP_PROFILES.md) for the
three designed profiles. **They are designed but explicitly not yet distributed** — see that
document's own banner and `AURA-PROTOCOL-V2-00` §24, §34.

A consumer project that wants to opt in (once bootstraps are actually distributed) needs only
two facts, however it stores them (its own README, its own agent instructions, etc.):

```text
AURA_ENABLED = true
PROJECT_CODE = <that project's sCodigo>
```

...plus a way to reach `GET /api/aura-protocol` from wherever its agents run. No other physical
convention is imposed — this repo's own `rules/AURA.md` + root `CLAUDE.md` pairing is one
concrete instance of that pattern, not a name every consumer must replicate literally.

## 5. Compatibility strategy for v2.1 / v3.0

- **Minor versions (v2.x)**: additive only — new optional fields, new event types, new roles'
  read-only privileges. A `CODING_AGENT` built against v2.0 must keep working unmodified against
  v2.1 (verified: every pre-existing test kept passing once retrofitted with a bearer token and
  lease threading — no v2.0 contract field or behavior was removed or changed). In practice,
  v2.0→v2.1 added enough new material (authentication, leases) to warrant its own file
  (`docs/aura/AURA_PROTOCOL_v2.1.md`) with a `SUPERSEDED BY` banner added to `v2.md`, rather than
  editing v2.0's file in place — a judgment call left to whoever ships the next minor version,
  not a hard rule. Either way: `AuraProtocolMetadataProvider.Current.Version`/`SourceDocument`/
  `Supersedes` are updated together with the document, in the same change.
- **Major versions (v3.0)**: may change contracts in breaking ways. Must add its own
  `docs/aura/AURA_PROTOCOL_v3.md`, and v2.0's file gets the same obsolescence header prescribed
  in §6 below, with `SUPERSEDED BY: AURA_PROTOCOL v3.0`. `AuraProtocolMetadataProvider.Current`
  is updated to point `sourceDocument` at the new file and `supersedes` at
  `docs/aura/AURA_PROTOCOL_v2.md`.
- The bootstrap mechanism (§4) is designed so this never requires rewriting every consumer's
  bootstrap: a bootstrap only ever says "ask `/api/aura-protocol` what's current" — it never
  hardcodes "v2.0".

## 6. Marking a protocol obsolete (the standing procedure)

When a version is superseded:

1. Add this header block to the very top of its document:

   ```text
   STATUS: OBSOLETE
   SUPERSEDED BY: <new protocol name and version>
   ```

2. Do **not** delete the file or its history — historical protocols are audit trail.
3. Update `AuraProtocolMetadataProvider.Current.Supersedes` to include it.
4. From that point on, no agent may treat the obsolete version as an operational fallback.

`rules/AURA.md @ 2026-08-16.2` has already received this header as part of activating v2.0's
design (see its own top section) — its operational content is otherwise untouched, since v2.0
is not yet `ACTIVE` (§3) and nothing should lose its only working fallback prematurely.

## 7. AURA Project Memory versioning

`AURA_PROJECT_MEMORY` is a separate transversal protocol and is not a version of `AURA_PROTOCOL`. Its current specification is `docs/aura/AURA_PROJECT_MEMORY_v1.0.md`; its physical contracts and templates are in `docs/aura/project-memory/`.

| Artifact | Versioning authority | Current value |
|---|---|---|
| Operational protocol | `AuraProtocolMetadataProvider.Current` plus its source document | `AURA_PROTOCOL v2.1` (status remains `IMPLEMENTED_PENDING_PRODUCTION_VERIFICATION`) |
| Project Memory semantics | `AURA_PROJECT_MEMORY` source document | `v1.0` — `STABLE` after Etapa 3 integration |
| Project manifest structure | `schemas/project.schema.json` | `schemaVersion 1.0` |
| Local machine profile structure | `schemas/machine.schema.json` | `schemaVersion 1.0` |
| Reusable templates | `project-memory/README.md` and repository history | template set for Project Memory v1.0 |

`projectMemoryVersion` in `.aura/project.json` identifies Project Memory only. It must not be interpreted as the current `AURA_PROTOCOL` version. A future Project Memory v1.1 may add compatible optional fields; a v2.0 may require migration. Unknown future major versions must not be accepted silently.

Changing Project Memory semantics requires updating its source document, contracts/templates as applicable, and this cross-reference in one reviewed change. It does not require changing `AURA_PROTOCOL` metadata unless the operational protocol itself changes.
