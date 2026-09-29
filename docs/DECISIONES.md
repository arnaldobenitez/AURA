# Decisiones vigentes

Índice resumido. El primer identificador disponible es `DEC-0005`; no se encontraron decisiones previas numeradas en la documentación del repositorio.

| ID | Fecha | Decisión | Estado | Motivo breve | ADR relacionado |
|---|---|---|---|---|---|
| DEC-0001 | 2026-09-28 | Las nuevas credenciales `INGESTION` deben tener alcance explícito a uno o más proyectos del mismo tenant; el canal valida el scope en cada operación. Credenciales legacy permanecen temporalmente tenant-wide hasta asignación/revocación; rotar una legacy sin scope se bloquea. | accepted | Evitar que una clave SMT pueda registrar actividad en cualquier proyecto del tenant, preservando transición no destructiva. | `docs/aura/PROJECT_CREDENTIAL_ADMIN_DESIGN.md` |
| DEC-0002 | 2026-09-28 | El portal administrativo exige JWT firmado por login humano web (`aura:authSource=PORTAL_LOGIN`) más allowlist de IDs por tenant en configuración servidor-side. `X-Admin-Api-Key` permanece exclusivamente como canal backend de compatibilidad. | accepted | La plataforma no tenía un modelo de roles humano; la autorización debe ser individual, tenant-scoped y fail-closed, sin enviar clave estática al frontend. | `docs/aura/PROJECT_CREDENTIAL_ADMIN_DESIGN.md` |
| DEC-0003 | 2026-09-28 | Los proyectos se desactivan mediante estado `ARCHIVED`; no se eliminan físicamente. `ProjectCode` es inmutable una vez asignado; proyectos legacy con código nulo pueden recibirlo una sola vez. | accepted | Preservar integridad referencial e historial append-only y permitir administrar filas legacy. | `docs/aura/PROJECT_CREDENTIAL_ADMIN_DESIGN.md` |
| DEC-0004 | 2026-09-29 | Read-back `REGISTRATION_ONLY`: un evento existente consultado por credencial de otro tenant devuelve `403` (no `404`); `404` solo para `eventId` inexistente. Un evento con resolución `PROVISIONAL` (sin proyecto) puede leerse por cualquier credencial `INGESTION` activa del tenant; un evento resuelto exige scope del proyecto. | accepted | La suite de integración ejecutada detectó que el 404 cross-tenant violaba v2.3 §8 y que el scope check por `projectCode` rechazaba lecturas de eventos sin proyecto. | `docs/aura/AURA_AGENT_REGISTRATION_PROTOCOL_v2.3.md` §8 |

## Cambios de estado

Una decisión aceptada se reemplaza mediante una nueva entrada y referencia explícita; no se borra historia.
