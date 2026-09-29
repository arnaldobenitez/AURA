# Estado actual

Última actualización: 2026-09-28
Última sesión: `0086622b-09c2-44df-9614-5fd86d528476`
Estado general: ABM de proyectos y credenciales `INGESTION` con scope por proyecto implementado localmente; schema pendiente de aplicación controlada, integración SQL y smoke operativo. `AURA Agent Registration Protocol v2.3` sigue `IMPLEMENTED_PENDING_PRODUCTION_VERIFICATION`; producción no fue modificada.

## Resumen ejecutivo

Post Etapa 9, los cambios de Project Memory quedaron ordenados en commits lógicos y se completó el push
manual previo. Roadmap y contrato v2.2 quedaron en `19b87fb`; el adaptador de memoria, en `0193d1f`; la metadata 0.8.00,
en `4543e28`; y el cierre de commits, en `ed95dbd`. Después se publicó 0.9.00 en el IIS interno y se aplicó
el esquema ProjectControl de forma schema-only sobre la base AURA, sin semillas de proyectos ni de usuarios. El
read-back del tablero y del endpoint Roadmap devolvió HTTP 200. La producción pública no fue tocada.

## Implementado

- `tools/aura.ps1` ejecuta el CLI desde cualquier ubicación del repositorio sin modificar PATH ni instalar herramientas globales.
- `tools/smoke-project-memory.ps1` cubre init, validate, bootstrap, sesiones, Project Manager, historial y handoff usando temporales.
- `project init` detecta Core parcial sin `.aura/project.json` mediante `APM014`, no sobrescribe y recomienda revisión manual.
- Guía práctica en `docs/aura/AURA_PROJECT_MEMORY_ADOPTION.md`.
- `rules/AURA.md` declara explícitamente sus fuentes normativas actuales y la no prevalencia del contenido legacy.
- Tests de inicialización parcial y de protección documental.
- Saneamiento Git: siete commits lógicos (`eb84a8c`..`39e0e38`) ordenan todo el trabajo de Project Memory.
- Limpieza final: residuos `appversion.json` y `.claude/` resueltos sin commits ajenos ni borrados.
- `AURA.Web`: sesión sin cierre automático por tiempo en el cliente; historial de detalle más reciente primero
  con paginación de 10/20/30 elementos.
- ProjectControl Roadmap local: `ProProyectoEtapa` extendida con código/criterios/validador/estado warning,
  auditoría de revisiones del plan, progreso por etapa y aprobación formal por la credencial AI_ANALYST asignada.
- API: lectura Roadmap y configuración del plan con credencial HUMAN; el tablero deriva avance global desde hitos
  cerrados cuando existe Roadmap.
- UI: el botón de la celda Etapa actual y el botón "Evolución del RoadMap" del detalle abren el mismo diálogo.
- `AURA_PROTOCOL v2.2`, integración de agentes, perfiles de bootstrap y descubrimiento `/api/aura-protocol`
  describen lectura de Roadmap, `stageCode`, `stageOutcome` y validación designada.
- `AURA Agent Registration Protocol v2.3` agrega `REGISTRATION_ONLY` mediante credencial técnica
  `INGESTION`, tabla append-only `AgentRegistrationEvent`, idempotencia por `eventId`, read-back y
  `projectResolution=PROVISIONAL` sin crear autorización gobernada.
- Las credenciales `INGESTION` quedan excluidas de eventos gobernados; `POST /api/project-control/events`
  conserva el flujo v2.2.
- ABM local: páginas Administración → Proyectos y Credenciales de agentes; crea/actualiza/pausa/activa/
  archiva proyectos sin DELETE físico y gestiona credenciales técnicas `INGESTION`.
- `AgentCredentialProject` vincula credenciales nuevas a uno o más proyectos. Scope/tenant/proyecto
  activo, expiración y revocación se vuelven a validar en cada `REGISTRATION_ONLY` POST/read-back,
  incluyendo JWT ya emitidos. Credenciales previas quedan marcadas `LEGACY_TENANT_WIDE`; asignarles
  el primer proyecto las restringe atómicamente; la rotación legacy sin asociación falla cerrado.
- La creación/rotación muestra secreto una sola vez, almacena hash PBKDF2 y los GET no lo incluyen.
  `AuraAdminAudit` registra cambios sin secretos. El portal no usa `X-Admin-Api-Key`.
- Administración humana: JWT con origen firmado `PORTAL_LOGIN` + allowlist de usuario por tenant
  (`Admin:TenantAdminUserIds:<tenantId>`); configuración ausente deniega por defecto. Una credencial
  técnica `HUMAN` no puede acceder al ABM.
- Documentación ABM/auditoría/roadmap/decisiones/despliegue añadida en `docs/aura/` y `docs/`.
- `docs/Architecture/AGENT_PLATFORM_ARCHITECTURE_v1.md`: diseño completo de la Universal Agent
  Platform (UAP) — entidades Human/Agent/Credential/Capability/Session/Lease/Approval/Decision/
  Reference, catálogos extensibles de Provider/AgentType/Capability, bootstrap universal,
  compatibilidad V1, plan de migración en fases y roadmap. Estado: DRAFT, pendiente de
  aprobación humana; no se modificó código, autenticación, APIs ni esquema.
- Segunda revisión UAP (v1.0-RC1): incorpora `AgentHost` (instalación física), `Workspace`,
  capacidades declaradas vs. otorgadas vs. efectivas, fase `CONTEXT_READY` como fin formal del
  bootstrap, `ProjectRoster` (equipo permanente) con catálogo `ProjectRole`, y versionado
  detallado del agente (`AgentVersion` + `AgentVersionHistory`). Documento elevado a
  RELEASE CANDIDATE; la implementación sigue sin autorizar.
- Tercera revisión UAP (v1.0-RC2, candidata final): `AgentIdentity` como ancla estable
  (Agent pasa a ser encarnación versionable), `ContextSnapshot` como producto formal del
  cierre de sesión, `AgentManifest` publicado en bootstrap, Capability Negotiation por sesión,
  reserva arquitectónica de Event Bus (sin implementación), capa Governance con auditoría
  tipificada (`GovernanceOperation`), y separación AAIP/UAP con AAIP como contrato de
  integración independiente. Sin código modificado.

## En curso

Roadmap v0.1 está publicado en el IIS interno y su esquema está aplicado. El endpoint devuelve HTTP 200, pero el EXEC
actual de AURA aún no tiene etapas configuradas (`totalStages=0`); el plan y su AI_ANALYST designado siguen pendientes.
ABM y validación de scope están implementados localmente, pero su esquema aditivo aún no se aplicó.
La API local `localhost:5235` no respondió en esta sesión; no se emitieron eventos AURA ni se confirmó
el estado operativo de `SMT`.

## Pendiente inmediato

1. Autorización humana final de la Release Candidate UAP v1.0-RC2
   (`docs/Architecture/AGENT_PLATFORM_ARCHITECTURE_v1.md`), incluidas las 14 decisiones
   abiertas de su §21. Tras la autorización comienza F1 (catálogos + AgentIdentity/Agent +
   backfill).
2. Definir el Roadmap del EXEC de AURA mediante `PUT /api/project-control/projects/AURA/roadmap`, con etapas y credenciales AI_ANALYST activas asignadas.
3. Leer el Roadmap después de la configuración y verificar las etapas antes de que los agentes registren avances.
4. La publicación/migración de producción pública queda pendiente de autorización y smoke test independiente.
5. Para activar v2.3 en un entorno real: aplicar el esquema schema-only, publicar la API, crear una credencial
   `INGESTION`, registrar un evento de prueba y hacer read-back. Producción pública sigue en v2.2.
6. Las pruebas SQL de integración requieren una connection string de test para ejecutar los escenarios de
   persistencia real; actualmente están omitidas cuando no hay conectividad.
7. Revisar el diff schema-only y confirmar esquema real/duplicados del catálogo con DBA; crear y verificar
   backup antes de ejecutar `scripts/project-control-schema-update.ps1 -SchemaOnly -TargetServer <...> -BackupVerified`.
8. Provisionar en configuración servidor-side los IDs humanos tenant-admin reales; hoy el default es
   deny-all y no se añadió ningún usuario administrador automáticamente.
9. Desplegar primero en entorno de prueba, crear/reutilizar `SMT` (estado operacional no consultable en
   esta sesión), asignar credential scope, y verificar token, aceptación/read-back y rechazo cross-project.
10. API AURA local no disponible y no se dispone de credencial `INGESTION` para esta sesión: registro
    de REQUEST/PROGRESS/HANDOFF queda pendiente; no se inventaron eventos ni tokens.

## Bloqueos

La conexión SQL de test no está configurada: las pruebas de integración con persistencia (82) se omiten.
No se aplicó ninguna migración ni se ejecutó contra base local o producción. El API AURA local también
estuvo inaccesible (`localhost:5235`), por lo que quedan pendientes smoke/read-back v2.3 y la validación
operativa del proyecto SMT.

## Problemas conocidos

Los módulos opcionales `02-DECISIONES`, `03-BACKLOG` y `04-ARQUITECTURA` no están presentes y continúan siendo
mostrados como ausentes sin crearlos.
El índice de Project Manager es local a la máquina y no forma parte de la memoria portable del proyecto.
`AGENTS.md` no está presente; el validador lo informa como `APM006 INFO` y no invalida el Core.

## Decisiones recientes relevantes

La inicialización parcial se detecta y se rechaza de forma conservadora: no se implementa reparación automática,
no se inventa `projectId` y no se sobrescriben archivos existentes.
El launcher y el smoke test son scripts locales, sin red, base de datos, dependencias pesadas ni cambios de ambiente.

## Pruebas y validaciones

- `dotnet build AURA.sln --no-restore`: correcto, 0 errores y 0 warnings.
- `AURA.UnitTests`: 43 superados, incluyendo scope de registro y autorización tenant-admin fail-closed.
- `AURA.ProjectMemory.Tests`: 154 superados.
- `AURA.IntegrationTests`: 8 superados y 82 omitidos por ausencia de conexión SQL de test. Los escenarios SQL del ABM/scope compilan, pero no se ejecutaron contra SQL Server.
- `AURA.Web`: `npm run typecheck`, `npm run lint` y `npm run build` correctos; oxlint 0 warnings/errores.
- El migrador schema-only compila y verifica tablas/columnas/índices del ABM; no fue ejecutado, no hubo backup ni cambios de base.
- AURA API local `localhost:5235` no respondió; no se hizo read-back/registro operativo de esta sesión.
- `git diff --check`: correcto; solo warnings de conversión LF/CRLF reportados por Git.

## Próximo paso recomendado

Antes de declarar PASS operativo del ABM: revisar la migración contra el esquema real, crear/verificar backup, provisionar tenant-admin humano en configuración segura, aplicar `SchemaOnly` en entorno autorizado, ejecutar integración SQL y smoke/read-back de `SMT` con credencial de prueba scope-limited. No ejecutar ni migrar producción sin autorización; el API público sigue bajo su estado operativo previo hasta verificación.
