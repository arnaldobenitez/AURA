# Auditoría del modelo actual — Proyectos y credenciales técnicas

**Fecha:** 2026-09-28  
**Alcance:** auditoría previa al ABM administrativo de proyectos y credenciales `INGESTION`.  
**Estado:** completada sobre contratos y código local; no hubo acceso a SQL ni a la API operativa.

## 1. Resultado ejecutivo

- Los proyectos se almacenan en el catálogo legacy `dbo.ProProyectosCabecera`; se identifican por `(idTenant, IdProyecto)` y el código (`sCodigo`) tiene índice único filtrado por tenant.
- El estado usa `nEstado`: `1=ACTIVE`, `2=PAUSED`, `5=ARCHIVED`; existe baja lógica mediante `ARCHIVED`, no borrado físico.
- `dbo.AgentCredential` está limitada por tenant, **no por proyecto**. La identidad técnica se autentica por `tenantId + credentialId + secret`; no existía una relación credencial-proyecto.
- El canal local v2.3 `REGISTRATION_ONLY` aceptaba una credencial `INGESTION` para cualquier `projectCode` del tenant y aceptaba proyectos desconocidos como `PROVISIONAL`.
- El login web (`POST /api/auth/login`) emite JWT `HUMAN`; no existía autorización administrativa basada en roles. `AdminApiKeyFilter` protegía todos los endpoints de administración con `X-Admin-Api-Key`.
- `Usuarios` es el registro humano legacy. El login web local fija tenant `1`; sus claims incluyen `actorType=HUMAN` y `LinkedActorId=IdUsuario`. No hay catálogo de roles ni asignación tenant-admin persistida.
- La historia actual es parcial: `AgentRegistrationEvent` es append-only para ingestión; Roadmap tiene revisiones; no existía una auditoría transversal para cambios administrativos de proyectos, credenciales o sus asignaciones.
- El proyecto `SMT` no aparece en la lista de semillas del migrador local. Sin conexión a la API/base operativa no se puede afirmar si ya está registrado; el ABM permite crearlo sin tocar otros proyectos.

## 2. Fuentes examinadas

- `docs/aura/AURA_AGENT_REGISTRATION_PROTOCOL_v2.3.md`
- `docs/aura/AGENT_INTEGRATION.md`
- `src/AURA.Api/Controllers/AgentCredentialAdminController.cs`
- `src/AURA.Api/Admin/AdminApiKeyFilter.cs`
- `src/AURA.Application/AgentCredential/`
- `src/AURA.Infrastructure/AgentCredential/`
- `src/AURA.Application/Registration/`
- `src/AURA.Infrastructure/Registration/`
- `src/AURA.Infrastructure/ProjectControl/ProjectControlSchemaMigrator.cs`
- `src/AURA.Infrastructure/ProjectControl/SqlProjectControlRepository.cs`
- `src/AURA.Application/HumanLogin/`
- `src/AURA.Infrastructure/HumanLogin/`
- `src/AURA.Api/Program.cs`, `src/AURA.Web/src/lib/auth.ts`, navegación y componentes del portal.

## 3. Modelo de proyectos existente

`ProProyectosCabecera` ya contiene `idTenant`, `IdProyecto`, `sNombre`, `sDescripcion`, `dFecCreacion`, `sPathTrabajo`, `nEstado`, `sCodigo`, `sRepositorio` y responsable humano. `sCodigo` puede ser NULL en filas legacy; la migración de ProjectControl define índice único filtrado `(idTenant, sCodigo)`. Los IDs se asignan por tenant. El ABM lista estas filas y permite asignar código una única vez, sin renombrar códigos existentes.

No se encontró una tabla `Tenant` canónica en las capas auditadas. El tenant es una partición numérica que aparece en PK/FK, claims JWT y contratos. Por ello:

- las rutas administrativas derivan el tenant del JWT humano, además de validar el tenant solicitado;
- los proyectos y sus relaciones se consultan siempre con el par tenant/id;
- los usuarios se autorizan solo en su tenant; el tenant administrador se provisiona en configuración segura, fail-closed;
- `ProjectCode` se vuelve inmutable luego de crear el proyecto para preservar referencias históricas.

`ExternalProjectKey` no existía en la cabecera de proyecto; sí existía como dato de evento de registro. El ABM lo agrega como metadata nullable del proyecto.

## 4. Modelo de credenciales existente

`dbo.AgentCredential` contiene `nActorTipo`, `sAgentName`, `LinkedActorId`, hash PBKDF2 con salt/iterations y marcas `CreatedAtUtc`, `RevokedAtUtc`, `LastUsedAtUtc`, `RotatedFromCredentialId`. El secreto plaintext se genera de manera criptográfica y solo se retorna en create/rotate. El repositorio de lectura administrativa no devuelve hash ni secret.

Antes de este cambio no había `ExpiresAtUtc`, auditoría de quién creó/modificó, `ScopeMode`, ni tabla credencial-proyecto. La credencial `INGESTION` autentica para el tenant completo. Un token JWT ya emitido no consultaba el estado de revocación en cada evento.

## 5. Autenticación humana y administración

`POST /api/auth/login` verifica email/password contra `Usuarios` y firma el mismo tipo base de JWT que las credenciales técnicas. `AURA.Web` usa ese endpoint y persiste su JWT humano en `localStorage`; no hay roles de aplicación almacenados.

El único control administrativo previo era `X-Admin-Api-Key`, protegido en tiempo constante, utilizado también para bootstrapping backend. No es apropiado enviarlo desde el frontend.

Esta implementación agrega una autorización humana para el portal: solo un JWT firmado con origen `PORTAL_LOGIN`, tipo `HUMAN`, tenant e ID humano incluidos, más una allowlist server-side `Admin:TenantAdminUserIds:<tenantId>`. Por defecto no hay administradores. La credencial técnica de tipo HUMAN no satisface el origen `PORTAL_LOGIN`. `X-Admin-Api-Key` permanece como canal backend de compatibilidad/break-glass y no se incorpora al frontend.

## 6. Auditoría existente y nueva

Antes, ProjectControl conservaba eventos y revisiones Roadmap, pero los cambios de catálogo/credenciales no tenían log funcional común.

El diseño implementado agrega `AuraAdminAudit` append-only con tenant, código de operación, tipo/ID de destino, usuario humano (nullable solo para el canal de compatibilidad server-to-server) y timestamp UTC. No existe campo para secreto, hash, password, token ni payload libre. Las asignaciones guardan quién asignó/revocó y sus timestamps.

## 7. Transición de alcance

- Filas antiguas de `AgentCredential` reciben `ScopeMode=LEGACY_TENANT_WIDE` por default durante la expansión aditiva. Esas credenciales conservan temporalmente su alcance histórico para no invalidar clientes en despliegues escalonados.
- Toda credencial `INGESTION` nueva se crea dentro de una transacción junto con una o más filas activas de `AgentCredentialProject` y `ScopeMode=PROJECT_SCOPED`.
- Al asignar el primer proyecto a una credencial legacy, la transición a `PROJECT_SCOPED` y la asignación son atómicas. Desde ese commit, ningún token antiguo de esa credencial puede escribir a proyectos no asignados.
- Rotar una credencial legacy tenant-wide sin asignaciones devuelve `PROJECT_SCOPE_REQUIRED`; el administrador debe asociar el proyecto antes de rotar. Nunca se crea otra credencial broad por rotación implícita.
- Revocación de credencial, asignación o proyecto (pausa/archive) es consultada en cada escritura/read-back `REGISTRATION_ONLY`, de modo que un JWT previamente emitido no conserva el permiso de proyecto.
- `PROVISIONAL` queda permitido solo para credenciales legacy tenant-wide. Las nuevas credenciales restringidas deben escribir a un proyecto registrado, activo y asignado.

## 8. Límites de la auditoría

La API local `localhost:5235` no respondió durante la auditoría; tampoco se obtuvo acceso a SQL Server. No se inspeccionaron filas ni esquema desplegado real. Por tanto, la migración está revisada como código pero no fue aplicada ni verificada contra base real; el estado operacional de `SMT` sigue sin confirmar.

## 9. Hallazgo de seguridad preexistente

En el código existente de inicialización del login humano se identificó una contraseña default literal. El valor se omite deliberadamente de esta documentación y de logs. No se modificó ese subsistema como parte del ABM; antes de exponer el portal en un entorno real, el operador debe revisar el bootstrap y rotar cualquier credencial humana afectada. La ruta `SchemaOnly` no ejecuta ese seed.
