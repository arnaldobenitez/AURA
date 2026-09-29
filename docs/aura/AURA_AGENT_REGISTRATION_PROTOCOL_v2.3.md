# AURA Agent Registration Protocol v2.3

**Estado:** implementado localmente — `IMPLEMENTED_PENDING_PRODUCTION_VERIFICATION`  
**Fecha:** 2026-09-27  
**Compatibilidad:** complementa `AURA_PROTOCOL v2.2`; no reemplaza `GOVERNED_EXECUTION`.  
**Fuente normativa externa:** `Contratos AURA con Agentes/AURA_Agent_Registration_Protocol_v2.3.md`.

> Copia controlada por el repositorio. El documento externo sigue siendo la fuente normativa
> designada; si difieren, corregir esta copia y la implementación en el mismo cambio.

## 1. Propósito

AURA registra actividad durable de cualquier proyecto, herramienta, agente o persona sin
convertir el registro en una barrera para comenzar el trabajo. El protocolo es universal para
TurnoMedix, SiniTrax, RoadScene, AURA, APIs, sitios, scripts, agentes de IA y herramientas
futuras.

## 2. Modos

### `REGISTRATION_ONLY`

Modo append-only para registrar movimientos. No exige identidad humana ni roles `HUMAN`,
`CODER` o `AI_ANALYST`; tampoco exige Roadmap, EXEC, `workStream`, `CLAIM`, lease, `HANDOFF`,
aprobación ni resolución previa de blockers.

`REGISTRATION_ONLY` nunca puede declarar una etapa aprobada, validada o completada ni alterar
control, autorización o ejecución.

### `GOVERNED_EXECUTION`

Modo de `AURA_PROTOCOL v2.2`, con roles, Roadmap, EXEC, `workStream`, `CLAIM`, leases,
`HANDOFF` y aprobación formal. Su fuente normativa sigue siendo
[AURA_PROTOCOL_v2.2.md](AURA_PROTOCOL_v2.2.md).

## 3. Autenticación

Las escrituras no son anónimas. Cada credencial técnica nueva `INGESTION` se crea desde el portal
administrativo o el endpoint administrativo compatible y debe asociarse a uno o más proyectos del
mismo tenant. La credencial autentica el canal/aplicación emisora; no identifica ni autoriza a una
persona y no otorga autoridad sobre `GOVERNED_EXECUTION`.

El portal requiere un JWT humano emitido por `POST /api/auth/login`, con origen `PORTAL_LOGIN`,
y el usuario debe estar explícitamente autorizado para ese tenant mediante la allowlist de servidor
`Admin:TenantAdminUserIds:<tenantId>`. No hay rol implícito por tener login. La configuración vacía
deniega acceso. `X-Admin-Api-Key` se conserva solo para compatibilidad backend/break-glass y nunca
se envía desde navegador.

```text
POST /api/admin/agent-credentials
Authorization: Bearer <JWT humano admin del portal>
{ "tenantId": 1, "actorType": "INGESTION", "agentName": "SMT-DEVIN", "projectIds": [<id de SMT>] }
```

El secreto se muestra una vez en la respuesta de create/rotate; se almacena solo hash PBKDF2. El
cliente puede configurar en su gestor de secretos `AURA_INGESTION_CREDENTIAL_ID`,
`AURA_INGESTION_SECRET`, `AURA_TENANT_ID` y `AURA_API_BASE_URL`. El nombre de variable es una
convención de integración del agente, no un claim ni parte de la autenticación.

## 4. Identidad declarada

`claimedActorType`, `claimedActorId`, `agentName`, `host`, `workspace`, `sessionId`, `model`,
`environment` y `source` son metadatos declarados por el emisor. Se almacenan con:

```text
identityStatus = UNVERIFIED
```

Nunca se convierten en claims de autorización ni habilitan operaciones gobernadas.

## 5. Endpoints

| Operación | Método y ruta | Autenticación |
|---|---|---|
| Registrar | `POST /api/agent-registration/events` | JWT de credencial `INGESTION`, proyecto activo asociado |
| Read-back | `GET /api/agent-registration/events/{eventId}?tenantId={tenantId}` | JWT `INGESTION` activo del mismo tenant y con scope del proyecto |

`GOVERNED_EXECUTION` permanece en `POST /api/project-control/events` y conserva su contrato
v2.2, sus leases y su cadena `expectedPreviousItemId`.

## 6. Evento de registro

Request mínimo:

```json
{
  "eventId": "uuid",
  "tenantId": 1,
  "projectCode": "TURNOMEDIX",
  "mode": "REGISTRATION_ONLY",
  "eventType": "PROGRESS",
  "summary": "Descripción breve",
  "details": "Descripción ampliada",
  "claimedActorType": "CODER",
  "claimedActorId": null,
  "agentName": "TURNOMEDIX-DEVIN-ARNALDO2",
  "host": "ARNALDO_2",
  "workspace": "TurnoMedix",
  "sessionId": "uuid",
  "model": "SWE-2-High",
  "environment": "Development",
  "source": "consumer",
  "occurredAtUtc": "2026-09-27T00:00:00Z",
  "externalProjectKey": null,
  "projectName": null,
  "identityStatus": "UNVERIFIED"
}
```

Campos persistidos:

- `eventId`, `tenantId`, `projectCode`, `mode`, `eventType`, `summary`, `details`;
- `claimedActorType`, `claimedActorId`, `agentName`, `host`, `workspace`, `sessionId`;
- `model`, `environment`, `source`, `occurredAtUtc`, `registeredAtUtc`;
- `externalProjectKey`, `projectName`, `projectResolution`;
- `identityStatus = UNVERIFIED`;
- `credentialId` de la credencial técnica autenticada.

Límites implementados: `summary <= 300`, `details <= 4000`, `workspace <= 300`,
`externalProjectKey`/`projectName <= 200`, y textos cortos <= 100. `projectCode <= 50` y
`claimedActorType <= 30`.

## 7. Tipos permitidos

`INFO`, `PROGRESS`, `DECISION`, `BLOCKER`, `HANDOFF`, `REQUEST`, `NOTE` y `ERROR`.

En este modo son declaraciones informativas. No cambian control, autorización, Roadmap, EXEC,
`workStream`, leases, aprobaciones ni estado de ejecución.

## 8. Idempotencia y read-back

`eventId` es único por `tenantId`. Una repetición devuelve HTTP `200` con:

```json
{
  "status": "ALREADY_EXISTS",
  "eventId": "uuid",
  "itemId": 123,
  "projectResolution": "RESOLVED"
}
```

No inserta una segunda fila ni modifica el evento original. El read-back devuelve el evento
persistido completo, con `itemId`, `projectId` cuando exista, `projectResolution`,
`identityStatus` y `credentialId`.

## 9. Proyectos resueltos y provisionales

- Para una credencial nueva `PROJECT_SCOPED`, `(tenantId, projectCode)` debe existir, estar `ACTIVE` y tener una asignación activa en `AgentCredentialProject`; de lo contrario el evento se rechaza con `403` y no se persiste.
- Solo las credenciales preexistentes identificadas explícitamente como `LEGACY_TENANT_WIDE` conservan la resolución provisional: si el proyecto no existe, el evento se acepta con `projectId = NULL` y `projectResolution = PROVISIONAL`.
- Un registro provisional no crea proyecto, autorización, EXEC, Roadmap ni control gobernado.
- La primera asignación de proyecto a una credencial legacy cambia atómicamente su scope a `PROJECT_SCOPED`; desde ese momento, los tokens ya emitidos también quedan sujetos a la lista vigente.

## 10. Sanitización y rechazo de secretos

El servicio rechaza antes de persistir material sensible en cualquier campo textual del
request. Los controles cubren passwords, secrets, API/admin keys, JWT, `Bearer`, connection
strings, tokens/firmas XML de ARCA/WSAA y claves privadas. Los valores sensibles no se escriben
en logs ni se devuelven en respuestas.

## 11. Persistencia

Los eventos se guardan en `dbo.AgentRegistrationEvent`, una tabla separada de
`ProProyectosItems`. `dbo.AgentCredentialProject` persiste las asignaciones activas/históricas;
`dbo.AuraAdminAudit` es el registro append-only de operaciones administrativas. La tabla de eventos
es append-only por contrato de aplicación y define:

- `UQ_AgentRegistrationEvent_Tenant_Event` sobre `(idTenant, IdEvento)`;
- checks de `mode`, `identityStatus`, `projectResolution` y tipos permitidos;
- FK al `AgentCredential` técnico autenticado;
- índice por tenant/proyecto/fecha para consulta.

No hay update ni delete en el repositorio de registro.

## 12. Regla principal

**AURA registra el trabajo; no impide que el trabajo comience.**

La gobernanza formal puede realizarse posteriormente cuando existan identidad HUMAN,
AI_ANALYST, Roadmap, EXEC, lease, criterios y aprobaciones.

## 13. Activación

La implementación local no implica despliegue ni activación productiva. Requiere backup verificado,
aplicar el esquema schema-only mediante el script explícito, desplegar API/portal, configurar la
allowlist de humanos administradores, asociar proyectos a credenciales `INGESTION`, ejecutar smoke
test y read-back controlado. Mientras no exista ese read-back, producción debe considerarse en v2.2.

Las credenciales `INGESTION` creadas desde el ABM requieren `projectIds`. `POST /api/auth/token`
emite el JWT; el alcance no se confía al token: el servidor vuelve a validar credencial, expiración,
estado del proyecto y asignación activa en cada evento y read-back. Las credenciales históricas sin
asignación quedan marcadas `LEGACY_TENANT_WIDE`; al asociar el primer proyecto, el sistema las pasa
a `PROJECT_SCOPED` inmediatamente. La rotación de una credencial legacy sin asociación requiere
primero asignar el proyecto. Las nuevas credenciales nunca se crean tenant-wide.
