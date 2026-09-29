# Diseño e implementación — ABM de Proyectos y Credenciales de Agentes

**Versión:** 1.0  
**Fecha:** 2026-09-28  
**Estado:** implementado localmente; requiere migración schema-only, pruebas con SQL y despliegue controlado.  
**Alcance:** AURA Core; no modifica proyectos consumidores.

## 1. Decisión de autorización

No había roles administrativos humanos en la plataforma. El portal no usa `X-Admin-Api-Key`. La autorización administrativa se deriva de un JWT firmado con:

- `aura:actorType = HUMAN`;
- `aura:authSource = PORTAL_LOGIN` (claim emitido por `POST /api/auth/login`, no por una credencial técnica HUMAN);
- `aura:tenantId` y `aura:linkedActorId` validados;
- ID humano presente en configuración server-side `Admin:TenantAdminUserIds:<tenantId>`.

Ejemplo no secreto de configuración: `Admin:TenantAdminUserIds:1 = 7,8`. En variables de entorno .NET: `Admin__TenantAdminUserIds__1=7,8`. El valor `7,8` es ilustrativo: el operador debe cargar los IDs reales autorizados. Sin allowlist, el acceso humano se deniega. La clave `Admin:ApiKey` solo se admite como compatibilidad/backend break-glass; jamás aparece en UI o JavaScript.

## 2. Administración de proyectos

Se reutiliza `ProProyectosCabecera` en vez de crear un catálogo paralelo. El código es único por tenant e inmutable luego de asignarse; un proyecto legacy con código nulo puede recibirlo una sola vez. Se exponen list/create/update/activate/pause/deactivate. `deactivate` es baja lógica con estado `ARCHIVED`; no hay DELETE físico.

Campos aditivos: `sExternalProjectKey`, `dFecModificacion`, `nUsuarioCreador`, `nUsuarioModificacion`. El estado usa el catálogo legacy (`1 ACTIVE`, `2 PAUSED`, `5 ARCHIVED`). Una creación administrativa no corre semillas globales y no modifica proyectos vecinos.

## 3. Credenciales y scope

`AgentCredential` conserva PBKDF2 y el patrón `credentialId + secret`, y agrega metadata de expiración/creación/actualización y `ScopeMode`. La relación `AgentCredentialProject` guarda el historial de asignación/revocación; su índice único filtrado impide más de una asignación activa por `(tenant, credential, project)`.

`PROJECT_SCOPED` es el modo obligatorio de toda credencial `INGESTION` creada/rotada por el nuevo flujo: exige al menos un proyecto del mismo tenant y estado activo. La autenticación intercambia la credencial por JWT; cada POST y read-back consulta nuevamente credencial activa, expiración, proyecto activo y asignación. Así la revocación o cambio de scope invalida también JWTs ya emitidos para el canal de registro.

`LEGACY_TENANT_WIDE` se usa únicamente en filas preexistentes tras migración. Conserva temporalmente compatibilidad. El primer attach cambia modo + agrega asignación en una transacción. La rotación de credencial legacy sin proyecto asociado falla con `PROJECT_SCOPE_REQUIRED`. La política futura recomendada es asignar proyectos explícitos y rotar las credenciales legacy antes de retirar el modo.

## 4. Secreto de un solo uso

- AURA genera 32 bytes aleatorios y calcula PBKDF2 salt/hash/iterations.
- La tabla recibe salt y hash únicamente; la transacción jamás persiste plaintext.
- La respuesta de create/rotate contiene `secret` una sola vez. Ningún GET/DTO de listado tiene esa propiedad.
- `AuraAdminAudit` registra tipo de operación, destino, tenant, actor humano opcional y fecha, no request/body, secreto, hash, password ni JWT.
- El frontend mantiene el plaintext solo en memoria React hasta que se cierra el panel; no usa `localStorage` ni `sessionStorage`. Copiar es una acción explícita.
- Variables para el agente: `AURA_INGESTION_CREDENTIAL_ID`, `AURA_INGESTION_SECRET`, `AURA_TENANT_ID`, `AURA_API_BASE_URL`; el secreto se configura en el secret manager del agente.

## 5. Contratos HTTP

Rutas administrativas protegidas por `AuraTenantAdmin` (JWT humano portal) o, solo para backend, `X-Admin-Api-Key`:

```text
GET    /api/admin/authorization
GET    /api/admin/projects?tenantId={tenantId}
POST   /api/admin/projects
PUT    /api/admin/projects/{projectId}
POST   /api/admin/projects/{projectId}/activate?tenantId={tenantId}
POST   /api/admin/projects/{projectId}/pause?tenantId={tenantId}
POST   /api/admin/projects/{projectId}/deactivate?tenantId={tenantId}
GET    /api/admin/agent-credentials?tenantId={tenantId}
POST   /api/admin/agent-credentials
POST   /api/admin/agent-credentials/{credentialId}/rotate?tenantId={tenantId}
POST   /api/admin/agent-credentials/{credentialId}/revoke?tenantId={tenantId}
GET    /api/admin/agent-credentials/{credentialId}/projects?tenantId={tenantId}
POST   /api/admin/agent-credentials/{credentialId}/projects
DELETE /api/admin/agent-credentials/{credentialId}/projects/{projectId}?tenantId={tenantId}
```

Creación técnica requiere `{ tenantId, agentName, actorType: "INGESTION", projectIds: [..], expiresAtUtc? }`. Los handlers comparan tenant solicitado con tenant del JWT antes de leer/escribir. Los códigos de error distinguen 401 (sin sesión), 403 (sin rol/tenant/scope), 404 (no existe), 409 (duplicación o requisito de scope) y 400 (validación).

## 6. Pruebas y limitaciones

Hay tests unitarios para policy administrativa y scope de registro, y tests SQL de integración para proyecto, credencial, one-time secret, listado sin secreto, scope, rotación, revocación y aislamiento. Estos últimos requieren SQL Server y se omiten cuando no existe conexión.

La aplicación de esquema **no se ejecuta** durante esta entrega. La comprobación final del token, evento SMT y denegación de otro proyecto exige aplicar el esquema en entorno de test, desplegar API, configurar un tenant-admin y ejecutar smoke/read-back controlados.
