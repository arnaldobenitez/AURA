> **Nota:** este informe (auditoría/diseño en borrador, nunca comiteado) queda superado en su
> contenido de protocolo por el informe final de la tarea `AURA-PROTOCOL-V2-00`
> (`docs/AURA-PROTOCOL-V2-00-Informe.md`) y por la especificación en `docs/aura/`. Se conserva
> sin modificar como registro histórico del trabajo previo; varias afirmaciones aquí (p. ej.
> sobre el campo `agent`, la lista de eventos, o las restricciones por actor) fueron corregidas
> tras verificación directa contra el código real — ver el informe final para el detalle.

# AURA-RECOVERY-SINITRAX-01 — Informe de auditoría y diseño de AURA_PROTOCOL v2.0

## A. Repo/HEAD inicial

- **Ruta:** `D:\Desarrollos\AURA`
- **Rama:** `master`
- **HEAD inicial:** `97e0790 (HEAD -> master) feat(project-control): creación de proyectos, historial y estados EXEC/etapa`
- **Remoto:** ningún remote configurado.
- **Working tree inicial:**
  - ` M appversion.json` (preexistente)
  - Sin cambios inesperados.
- **Versión del producto:** `0.5.00` (canal `Alpha`)
- **Fecha de publicación:** `2026-08-20 14:52:09`

## B. Arquitectura AURA encontrada

### B.1 Tecnología

- **Backend:** .NET 10 ASP.NET Core Web API (proyectos `AURA.Api`, `AURA.Application`, `AURA.Domain`, `AURA.Infrastructure`).
- **Frontend:** React + TypeScript + Vite (`AURA.Web`).
- **Persistencia:** SQL Server, EF Core (migraciones manuales, no automáticas).
- **Control de proyectos IA:** SQL directo sobre tablas heredadas `ProProyectos*` + tablas nuevas `ProProyectoEXEC` y `ProProyectoEtapa`.
- **Autenticación:** ninguna en los endpoints de control de proyectos actualmente.

### B.2 Estructura de capas

- `AURA.Domain`: entidades y reglas (aún sin contenido de negocio; solo enums de `ProjectControl`).
- `AURA.Application`: casos de uso, contratos (`ProjectControlService`, DTOs, requests, results).
- `AURA.Infrastructure`: EF Core, SQL directo, schema migrator.
- `AURA.Api`: controladores, health, middleware.
- `AURA.Web`: UI en React.
- `tests`: unitarios e integración.

### B.3 Endpoints de Project Control

- `GET /api/project-control?tenantId={n}`: tablero con proyectos, último evento, control y avances.
- `GET /api/project-control/projects/{projectCode}/events?tenantId={n}`: historial de eventos.
- `POST /api/project-control/projects`: crea un proyecto.
- `POST /api/project-control/events`: registra un evento (idempotente por `eventId`, control optimista con `expectedPreviousItemId`).

### B.4 Contrato de registro de eventos

```json
{
  "tenantId": 1,
  "projectCode": "AURA",
  "eventId": "<UUID>",
  "expectedPreviousItemId": 0,
  "eventType": "CLAIM | PROGRESS | HANDOFF | BLOCKER | COMPLETION",
  "actor": { "type": "CODER | AI_ANALYST | HUMAN | UNASSIGNED", "id": null },
  "agent": "VSCode/Sonnet | ChatGPT | Devin | null",
  "environment": "VSCode | ChatGPT | Windsurf | null",
  "status": "IN_PROGRESS | BLOCKED | COMPLETED",
  "summary": "...",
  "details": "...",
  "stage": "...",
  "stageProgress": 0,
  "totalStages": 0,
  "nextControl": { "type": "...", "id": null },
  "nextAction": "...",
  "blocker": "...",
  "reference": "...",
  "model": "Sonnet | Codex | GPT-4 | null"
}
```

## C. Modelo de datos

### C.1 Tablas principales heredadas

- `ProProyectosCabecera`: proyectos (`IdProyecto`, `sNombre`, `sCodigo`, `sRepositorio`, `nHumanoResponsable`).
- `ProProyectosItems`: eventos/itineraciones (`IdItem`, `IdProyecto`, `dFecha`, `sObservaciones` más nuevas columnas de evento).
- `ProCodificadores`: catálogo de agentes/codificadores.
- `ProProyectosAnalistas`: analistas.
- `Usuarios`: humanos.

### C.2 Tablas nuevas

- `ProProyectoEXEC`: EXEC con `nEstado`, `nTotalEtapas`, `sDescripcion`.
- `ProProyectoEtapa`: etapas con `nOrden`, `sDescripcion`, `nPorcEtapa`, `nEstado`.

### C.3 Columnas de evento (ProProyectosItems)

- `IdEvento` (UUID idempotencia).
- `IdItemAnterior` (cadena histórica).
- `nTipoEvento` (CLAIM=1, PROGRESS=2, HANDOFF=3, BLOCKER=4, COMPLETION=5).
- `nActorTipo`, `nCodificador`/`nAnalista`/`nHumano` (actor heredado).
- `nEstadoTrabajo` (IN_PROGRESS=1, BLOCKED=2, COMPLETED=3).
- `nControlTipo`, `nControlId` (quién tiene el control).
- `sResumen`, `sProximaAccion`, `sBloqueo`, `sReferencia`, `sModelo`.
- `nPorcEXEC`, `nPorcEtapa`, `sEtapa`, `nTotalEtapas`.
- `nAgente` (enum), `sEntorno`, `sAgente` (texto libre), `IdEXEC`, `IdEtapa`, `IdControlItem`.

### C.4 Identidad de actor

Actores heredados:

- `nCodificador` (ProCodificadores)
- `nAnalista` (ProProyectosAnalistas)
- `nHumano` (Usuarios)

Nuevos campos:

- `nAgente` (enum: `1 = CODER`, `2 = AI_ANALYST`, `3 = HUMAN`, `4 = UNASSIGNED`)
- `nControlId` + `nControlTipo` para el control.

## D. API real

### D.1 Base local

- `http://localhost:5235`
- `GET /health`, `/health/live`, `/health/ready`
- OpenAPI: `/openapi/v1.json` (Development)

### D.2 Base productiva

- Se menciona `https://aura.aguarapp.com` en el prompt.
- No se encontró configuración productiva en el repo.
- No se realizaron requests productivos en esta auditoría.

### D.3 Autenticación

- No hay autenticación en endpoints de control de proyectos.
- README advierte que no deben exponerse fuera de entorno controlado.

## E. Autenticación/Autorización

- Sin autenticación en API de control de proyectos.
- Secretos: via User Secrets / variables de entorno (`AURA_DESIGNTIME_CONNSTR`).
- `appsettings.json` y `appsettings.Development.json` no contienen cadenas de conexión.

## F. Protocolos históricos encontrados

| Archivo | Versión | Destinatario | Estado | Contenido útil | Conflictos | Acción propuesta |
|---|---|---|---|---|---|---|
| `rules/AURA.md` | 2026-08-16.2 | Todos | **CURRENT** (vigente previo a v2.0) | Reglas de identidad, EXEC, eventos, validación, read-back | Limitado a `localhost:5235`; no distingue perfiles detallados ni workstreams | Migrar a v2.0, marcar como SUPERSEDED |
| `AURA-Technical-Foundation.md` | — | Desarrolladores | LEGACY | Arquitectura técnica | Puede contener decisiones obsoletas | Revisar, conservar útil, actualizar si aplica |
| `AURA-Development-Plan.md` | — | Desarrolladores | LEGACY | Roadmap y plan | Puede estar desactualizado | Revisar, conservar útil |
| `AURA-Project-Status.md` | — | Desarrolladores | LEGACY | Estado del proyecto | Desactualizado probablemente | Revisar, archivar o actualizar |
| `AURA-VSCode-Prompt-Template.md` | — | VS Code / Sonnet | LEGACY | Prompt template | Referencia local de implementación | Revisar, migrar conceptos al profile v2.0 |
| `docs/` varios | — | — | LEGACY | Documentación general | — | Revisar |
| `Versiones/0.2.00..0.5.00` | 0.2.00 - 0.5.00 | Binarios publicados | ARCHIVE | Historial de publicaciones | No son fuente de verdad del protocolo | Conservar como histórico |

**No se encontraron archivos explícitos** `AURA_PROTOCOL v1.0`, `v1.1`, `v1.1-CA`. El protocolo más cercano a una especificación formal es `rules/AURA.md`.

## G. Problemas de divergencia

1. **Múltiples agentes no registran identidad consistente.** `rules/AURA.md` define actores ChatGPT, Devin, Codex, Grok, etc. con IDs, pero no incluye VS Code/Sonnet/Cursor/Windsurf como actores. `RegisterProjectEventRequest` permite `agent` y `environment` en texto libre, lo que permite inconsistencias.
2. **No distinción explícita entre CONTROL y AGENT.** El DTO incluye `nextControl` (tipo+id) y `agent`/`environment`/`model` como cadenas, pero no hay un campo normalizado `CONTROL` separado de `AGENT`.
3. **No hay modelo de WORKSTREAM.** La tabla `ProProyectosCabecera` representa proyectos (`SiniTrax`, `AURA`, etc.), pero no subproyectos (`AFD`, `WhatsApp`, `Email/Tickets`, `PdfImport`). `SiniTrax` necesita workstreams concurrentes.
4. **Los porcentajes PROJECT/EXEC no están claramente diferenciados.** `ProjectControlBoardDto` expone `StageProgress` y `ExecProgress`, pero no `ProjectProgress` como concepto separado. `rules/AURA.md` menciona `execProgress` calculado sobre etapas cerradas.
5. **El protocolo vigente apunta a `localhost`.** `rules/AURA.md` referencia `http://localhost:5235` y `http://localhost:5173`. No hay descubrimiento de la URL productiva.
6. **Falta bootstrap para nuevos agentes.** Ningún agente nuevo puede descubrir automáticamente el contrato correcto.
7. **No hay mecanismo de concurrencia visible.** Aunque `expectedPreviousItemId` ayuda a detectar conflictos, no hay una regla explícita de "un único CONTROL activo por proyecto/etapa/workstream".
8. **No hay idempotencia en `GET`.** El POST es idempotente por `eventId`.
9. **Estados son limitados:** `IN_PROGRESS`, `BLOCKED`, `COMPLETED`. Faltan `PLANNED` y `WAITING_REVIEW`.
10. **Falta `HUMAN` como control seguro por defecto.** `rules/AURA.md` ya lo recomienda.

## H. Modelo v2.0 — Conceptos

### H.1 Conceptos centrales (CORE)

```text
PROJECT         = ProProyectosCabecera
WORKSTREAM      = subdivisión de PROJECT (AFD, WhatsApp, etc.) [NO EXISTE AÚN]
EXEC            = ProProyectoEXEC
STAGE           = ProProyectoEtapa
ITEM            = ProProyectosItems (evento/intervención)
ACTOR           = quien ejecuta (analyst, coder, human)
AGENT           = instancia concreta (ChatGPT, Sonnet, Devin)
CONTROL         = quien debe continuar
STATUS          = IN_PROGRESS | BLOCKED | COMPLETED | WAITING_REVIEW | PLANNED
EVENT           = CLAIM | PROGRESS | HANDOFF | BLOCKER | COMPLETION | REVIEW
STAGE_PROGRESS  = nPorcEtapa (0-100)
EXEC_PROGRESS   = nPorcEXEC (etapas cerradas / total)
PROJECT_PROGRESS= ponderación de EXECs/workstreams
```

### H.2 Mapeo al modelo real

| Concepto v2.0 | Campo real actual | Nota |
|---|---|---|
| `PROJECT` | `ProProyectosCabecera.IdProyecto` + `sCodigo` | Existe. |
| `WORKSTREAM` | No existe. | Necesario agregar o derivar. |
| `EXEC` | `ProProyectoEXEC.IdEXEC` | Existe. |
| `STAGE` | `ProProyectoEtapa.IdEtapa` | Existe. |
| `ACTOR` | `nAgente` + `nCodificador`/`nAnalista`/`nHumano` | Parcial. |
| `AGENT` | `sAgente`, `sEntorno`, `sModelo` | Texto libre. |
| `CONTROL` | `nControlTipo` + `nControlId` | Existe. |
| `STATUS` | `nEstadoTrabajo` | Limitado. |
| `STAGE_PROGRESS` | `nPorcEtapa` | Existe. |
| `EXEC_PROGRESS` | `nPorcEXEC` | Existe. |
| `PROJECT_PROGRESS` | No existe. | Debe derivarse de workstreams/EXECs. |
| `HANDOFF` | `HANDOFF` evento + `nextControl` | Existe. |
| `READ-BACK` | `GET /api/project-control?tenantId=1` y `GET .../events` | Existe. |

## I. CORE — AURA_PROTOCOL v2.0

### I.1 Objetivo

Unificar la coordinación multiagente para proyectos AURA, eliminando la dependencia de memorias locales, instrucciones personales, endpoints `localhost` y versiones contradictorias del protocolo.

### I.2 Fuentes de verdad

1. **Protocolo versionado:** `docs/AURA_PROTOCOL_v2.0.md` (este entregable).
2. **Contrato API:** OpenAPI publicado por `AURA.Api` (`/openapi/v1.json` en desarrollo).
3. **Estado operativo:** API `GET /api/project-control?tenantId=1`.

### I.3 Versionado

```text
ProtocolName:   AURA_PROTOCOL
MajorVersion:   2
MinorVersion:   0
PublishedAt:    2026-08-25
Compatibility:  All agents
Supersedes:     rules/AURA.md 2026-08-16.2
                AURA-Technical-Foundation (when contradictory)
Status:         DESIGNED / PENDING IMPLEMENTATION
```

### I.4 Ciclo de vida universal

#### Inicio de trabajo

1. Identificar `projectCode` y, si existe, `workStream`.
2. Consultar tablero: `GET /api/project-control?tenantId=1`.
3. Localizar proyecto y último `lastItemId`.
4. Verificar `currentControl`.
5. Si no hay conflicto, generar `eventId` (UUID).
6. Enviar `CLAIM` (o `PROGRESS` si ya se tiene control).
7. Leer de vuelta y confirmar sincronización.

#### Durante la ejecución

- Registrar `PROGRESS` solo ante hitos significativos (no por cada comando).
- Actualizar `stage`, `stageProgress`, `execProgress`, `summary`, `reference`, `model`, `agent`, `environment`.

#### Cierre / handoff

1. Registrar `HANDOFF` o `COMPLETION`.
2. Incluir `nextControl` explícito.
3. Si no hay sucesor confirmado, usar `HUMAN` id `1`.
4. Incluir `reference` (commit, tarea, PR).
5. Leer de vuelta (`READ-BACK`) y confirmar.

### I.5 Handoff

- Siempre explícito.
- `nextControl.type` + `nextControl.id` requeridos.
- `nextAction` requerido (a menos que sea `COMPLETION`).

### I.6 Read-back

Después de cada `POST` crítico:

- `GET /api/project-control?tenantId=1`.
- Verificar `lastItemId`, `stage`, `stageProgress`, `execProgress`, `currentControl`, `lastAgent`, `lastModel`.
- Si no coincide: `AURA_SYNCHRONIZATION = FAILED`.

### I.7 Concurrencia

- Regla: **un único `CONTROL` activo por (`projectCode`, `workStream`)**.
- Si un agente intenta `CLAIM` cuando otro controla: el POST debe detectar conflicto (`expectedPreviousItemId`) y devolver `409`. El agente debe consultar de nuevo.
- Para módulos distintos, recomendar `workStream` separado en lugar de `project` separado.

### I.8 Idempotencia

- `POST /api/project-control/events` es idempotente por `eventId`.
- Reintentos con mismo `eventId` devuelven `200 ALREADY_EXISTS`.
- `expectedPreviousItemId` evita duplicados inadvertidos por concurrencia.

### I.9 Contingencia si AURA no disponible

- Declarar `AURA_SYNCHRONIZATION = PENDING`.
- Registrar localmente: proyecto, etapa, agente, EXEC, PROJECT conocido, resultado, commit, timestamp, siguiente control.
- No inventar sincronización.
- No volver a protocolos v1.x.

## J. Profile — Analyst

### J.1 Responsabilidades

- Diseñar/validar etapas.
- Iniciar EXECs.
- Aprobar/rechazar cierres de etapa.
- Definir `PROJECT`/`EXEC` ponderación.
- Asignar `CONTROL` al siguiente agente.
- No aprobar implementación técnica sin evidencia del ejecutor.

### J.2 Comportamiento

- `AI_ANALYST` actor `1` (ChatGPT analizando) o humano analista.
- Puede inicializar `stageProgress = 0`, `execProgress = 0`.
- Cierra etapas al `100%` con `HANDOFF` a `HUMAN` o `CODER`.

## K. Profile — Coding Agent

### K.1 Responsabilidades

- Inspeccionar repo.
- Implementar, probar, compilar, commit (autorizado).
- Actualizar `stageProgress`.
- No redefinir `PROJECT`/`EXEC`.
- No aprobar funcionalmente su propio trabajo.

### K.2 Comportamiento

- `CODER` actor con ID según catálogo: ChatGPT (`3`), Codex (`4`), Grok (`5`), Devin (`2`), etc.
- Si no está registrado: `UNASSIGNED` + informar.
- `agent` = `VSCode/Sonnet`, `Codex`, etc.
- `model` = `Sonnet 5`, `GPT-4`, etc.
- `environment` = `VSCode | Windsurf | Cursor`.

## L. Profile — Autonomous Agent

### L.1 Responsabilidades

- Similares a Coding Agent.
- Puede ejecutar flujos más largos.
- Debe respetar `CONTROL`, `EXEC`, `PROJECT`, handoff, read-back, límites de Git.

### L.2 Comportamiento

- `agent` = `Devin`, `Windsurf`, etc.
- `environment` = `Windsurf` | `Devin`.
- No implica autonomía de producto.

## M. EXEC / PROJECT

### M.1 EXEC

- Porcentaje de la **etapa actual** o del **EXEC actual**.
- `execProgress = etapas cerradas / total de etapas aprobadas * 100`.
- No sube por avance parcial de etapa actual.

### M.2 PROJECT

- Porcentaje **global** del proyecto/subproyecto.
- Proviene de roadmap aprobado, ponderación de EXECs/workstreams o indicación del Analista.
- Ningún agente aumenta `PROJECT` arbitrariamente.
- Si no puede determinarse, mantener sin cambios.

### M.3 WORKSTREAM

- Necesario para SiniTrax.
- `AFD` es un workstream cerrado al `100%`.
- `SiniTrax` no es `100%`.
- Si el modelo actual no permite workstreams, documentar limitación y proponer extensión mínima (tabla `ProProyectoWorkStream` o campo `sWorkStream` en `ProProyectoEXEC`).

## N. CONTROL / AGENT

### N.1 CONTROL

- Rol operacional: `AI_ANALYST`, `CODER`, `HUMAN`, `NONE`.
- Representado por `nControlTipo` + `nControlId`.
- "¿Quién tiene el control del proyecto/workstream ahora?"

### N.2 AGENT

- Instancia concreta: `VSCode/Sonnet`, `ChatGPT`, `Devin`, `Windsurf`, `Cursor`.
- Representado por `agent`/`environment`/`model` en texto libre.
- Distinto de `CONTROL`.

## O. Handoff

- Evento `HANDOFF` con `nextControl` explícito.
- Cambia `CONTROL`.
- No implica aprobación automática.

## P. Read-back

- Obligatorio después de `POST` crítico.
- `GET /api/project-control` y `GET .../events`.
- Confirmar `AURA_SYNCHRONIZATION = CONFIRMED`.

## Q. Concurrencia

- `expectedPreviousItemId` como control optimista.
- Un solo `CONTROL` activo por (`projectCode`, `workStream`).
- Si un agente intenta tomar control ocupado: `409 Conflict`.
- Módulos diferentes usan `workStream`.

## R. Workstreams

### R.1 Necesidad

SiniTrax tiene trabajos concurrentes: `AFD`, `WhatsApp`, `Email/Tickets`, `PdfImport`, `Asegurados`, `Croquis`.

### R.2 Representación propuesta

Opción mínima: agregar `sWorkStream` a `ProProyectoEXEC` y `ProProyectosItems` (nullable para compatibilidad).

Alternativa: tabla `ProProyectoWorkStream`.

**Recomendación:** campo `sWorkStream` (100 chars) en `ProProyectoEXEC` y `ProProyectosItems`. Mantener `NULL` para proyectos sin subdivisión. No requiere FK nueva.

### R.3 Reconciliación SiniTrax

- `SiniTrax` proyecto global.
- `AFD` workstream: `PROJECT = 100%`, `STATUS = CLOSED`.
- Otros workstreams: no marcados como cerrados.
- Si no se implementa `workStream`, no escribir `SiniTrax = 100%`; documentar limitación.

## S. Contingencia

Si AURA no disponible:

1. Declarar `AURA_SYNCHRONIZATION = PENDING`.
2. Registrar localmente.
3. No usar SQL directo.
4. No volver a v1.x.
5. Informar al usuario en informe.

## T. Versionado

```text
AURA_PROTOCOL
2.0
2026-08-25
Supersedes: rules/AURA.md 2026-08-16.2
Status: DESIGNED / PENDING IMPLEMENTATION
```

## U. Cambios necesarios propuestos

1. **Documentación:** crear `docs/AURA_PROTOCOL_v2.0.md` y perfiles; marcar `rules/AURA.md` como `SUPERSEDED`.
2. **API:** extender `RegisterProjectEventRequest` con `workStream` (opcional, string). Sin romper contrato existente.
3. **Esquema:** agregar `sWorkStream` a `ProProyectoEXEC` y `ProProyectosItems` (nullable, sin FK).
4. **Board DTO:** incluir `workStream` en `ProjectControlBoardDto`.
5. **Estados:** extender `nEstadoTrabajo` con `WAITING_REVIEW` y `PLANNED` si se requiere.
6. **Bootstrap:** añadir endpoint `GET /api/aura-protocol` que devuelva metadata del protocolo vigente.
7. **Autenticación:** fuera de alcance de esta etapa, pero documentar como requerimiento futuro.

## V. Migraciones

- Propuesta mínima: script aditivo `project-control-schema-update.ps1` que agregue `sWorkStream` y estados nuevos.
- Sin borrar datos.
- Sin recrear tablas.
- Backup previo.

## W. Tests

Si se implementan cambios, cubrir:

- `GET /api/project-control` (board).
- `GET /api/project-control/projects/{code}/events`.
- `POST /api/project-control/events` (idempotencia).
- `POST /api/project-control/events` con `expectedPreviousItemId` erróneo (conflicto).
- Handoff con `nextControl`.
- Read-back.

## X. Build

Actualmente `0.5.00` compila en .NET 10. No se ejecutó build en esta etapa de auditoría/diseño. Recomendado antes de implementar.

## Y. Reconciliación SiniTrax

**No se realiza en esta etapa.** El prompt de trabajo AURA-RECOVERY-SINITRAX-01 establece explícitamente que primero debe completarse la auditoría y diseño del protocolo, y el Gate de Diseño requiere aprobación del Analista Funcional antes de implementar cambios o reconciliar datos productivos.

### Y.1 Historia AFD a reconciliar (cuando se apruebe)

- HEAD: `258bb74f02581084404e5e1c93b51e58695167d4`
- Etapas AFD-01 a AFD-07 con commits certificados.
- `AFD PROJECT = 100%`, `AFD STATUS = CLOSED`.
- `SiniTrax` no es `100%`.

## Z. Estado AFD en AURA

No se consultó base de datos ni API productiva en esta etapa. No se verifica el estado real.

## AA. Protocolos obsoletos

- `rules/AURA.md 2026-08-16.2`: marcar `SUPERSEDED BY AURA_PROTOCOL v2.0`.
- `AURA_PROTOCOL v1.x` y `AURA_PROTOCOL_CODING_AGENT v1.1-CA` (si existen en alguna otra ubicación): `OBSOLETE`.

## AB. Git final

- Commit previsto: `docs(aura): protocolo multiagente v2.0 y perfiles`
- No se ha realizado todavía (falta crear archivos y aprobación).

## AC. Riesgos pendientes

1. **API sin autenticación.** No exponer a producción pública.
2. **URL productiva no documentada en repo.** Necesario mecanismo de descubrimiento.
3. **Workstreams no implementados.** Sin ellos, SiniTrax no puede representar correctamente subproyectos concurrentes.
4. **Agentes no normalizados.** `agent`/`environment` son texto libre; puede haber inconsistencias.
5. **Sin pruebas de concurrencia reales.** La lógica `expectedPreviousItemId` requiere validación.
6. **No se realizó build ni request productivo.** Recomendado antes de aprobar v2.0.

## AD. Próximo paso recomendado

1. **Aprobar este informe con el Analista Funcional.**
2. **Crear archivos del protocolo** (`docs/AURA_PROTOCOL_v2.0.md` + perfiles compactos).
3. **Marcar obsoletos** protocolos anteriores (`rules/AURA.md` header).
4. **Implementar cambios mínimos** (workStream + bootstrap endpoint) con tests.
5. **Validar build y read-back.**
6. **Reconciliar SiniTrax/AFD** en AURA productiva (con backup).
7. **Distribuir perfiles** a ChatGPT/Analyst, VS Code/Sonnet, Windsurf/Devin.
