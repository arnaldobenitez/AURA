# Changelog

## Unreleased

### Added

- Implemented `AURA Agent Registration Protocol v2.3` locally with explicit
  `REGISTRATION_ONLY` mode. The additive channel is append-only, authenticated by a technical
  `INGESTION` `AgentCredential`, idempotent by `(tenantId, eventId)`, and supports read-back by
  `eventId`.
- Added `POST /api/agent-registration/events` and
  `GET /api/agent-registration/events/{eventId}?tenantId={tenantId}`.
- Added `dbo.AgentRegistrationEvent` to the schema-only migration path, separate from governed
  `ProProyectosItems`; unresolved projects persist as `PROVISIONAL` without creating governed
  authorization.
- Added sanitization and rejection for credential material in registration payloads, including
  passwords, secrets, JWTs, connection strings, admin API keys, ARCA/WSAA tokens, Bearer tokens,
  and private keys.
- Updated protocol discovery to report v2.3 while keeping `GOVERNED_EXECUTION` under the v2.2
  normative document.
- Added unit and integration test coverage for valid registration, idempotency, provisional
  projects, invalid/governed credentials, secret rejection, tenant isolation, event types, and
  non-mutation of governed board/EXEC/Roadmap/lease state.

### Notes

- `GOVERNED_EXECUTION` remains on `POST /api/project-control/events` and is unchanged.
- Production remains unverified for v2.3 until a controlled deployment and read-back are
  completed.
