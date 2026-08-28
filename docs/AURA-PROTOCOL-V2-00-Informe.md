# AURA-PROTOCOL-V2-00 — Informe final

## A. Estado inicial Git

- Ruta: `D:\Desarrollos\AURA`. Rama: `master`. Remoto: ninguno configurado.
- HEAD inicial: `97e0790 feat(project-control): creación de proyectos, historial y estados EXEC/etapa`.
- Working tree inicial: `M appversion.json` (preexistente, ajeno a esta tarea, no tocado) +
  dos archivos sin comitear de una sesión previa (`docs/AURA-RECOVERY-SINITRAX-01-Informe.md`,
  `docs/AURA_PROTOCOL_v2.0.md`) que eran un borrador incompleto del mismo objetivo. Se
  incorporó y corrigió su contenido en vez de descartarlo; ver sección E y H.
- Ningún cambio ajeno fue absorbido ni descartado.

## B. Arquitectura AURA encontrada

- Backend: .NET 10 ASP.NET Core (`AURA.Domain` → `AURA.Application` → `AURA.Infrastructure`/`AURA.Api`).
- Frontend: React + TypeScript + Vite (`AURA.Web`), no modificado en esta tarea.
- Persistencia: SQL Server vía SQL directo sobre tablas heredadas `ProProyectos*` + tablas
  nuevas `ProProyectoEXEC`/`ProProyectoEtapa` (no hay entidades EF Core de negocio).
- **Sin autenticación** en ningún endpoint de Control de Proyectos IA, en ningún entorno —
  confirmado en `Program.cs`, `ProjectControlController.cs` y `README.md`.
- Desarrollo: `http://localhost:5235`. Producción: `https://aura.aguarapp.com` (IIS, `AURA.Api`
  montada en `/api` bajo el sitio raíz, host OMEN).

## C. Modelo de datos relevante

`ProProyectosCabecera` (PROJECT, `ProjectStatus`: DRAFT/ACTIVE/PAUSED/BLOCKED/COMPLETED/ARCHIVED),
`ProProyectoEXEC` (EXEC, `nEstado`: Active/Completed), `ProProyectoEtapa` (STAGE, `nEstado`:
Open/Approved), `ProProyectosItems` (ITEM/evento, idempotente por `IdEvento`, con
`UPDLOCK, HOLDLOCK` para el siguiente id). No existe columna ni concepto de `WORKSTREAM` ni de
`PROJECT_PROGRESS` numérico en ningún lado del modelo real.

## D. API/integraciones existentes

`GET /api/project-control`, `GET /api/project-control/projects/{code}/events`,
`POST /api/project-control/projects`, `POST /api/project-control/events`,
`GET /health` (sin dependencia de SQL), `GET /health/ready` (depende de SQL Server),
`GET /openapi/v1.json` (solo Development). Contrato de evento con 9 tipos reales:
`REQUEST, CLAIM, PROGRESS, HANDOFF, BLOCKER, APPROVAL, REWORK, COMPLETION, DEPLOYMENT` —
más ricos que cualquier documentación previa (`rules/AURA.md` solo documentaba 5).

## E. Protocolos legacy encontrados

| Archivo | Estado detectado | Acción tomada |
|---|---|---|
| `rules/AURA.md @ 2026-08-16.2` | Vigente hasta ahora, con brechas reales (ver F) | Encabezado `STATUS: OBSOLETE / SUPERSEDED BY: AURA_PROTOCOL v2.0` agregado; contenido original intacto debajo. |
| `docs/AURA_PROTOCOL_v2.0.md` (borrador suelto, sin comitear) | Diseño incompleto de la misma v2.0, con varias afirmaciones no verificadas contra el código real | Reemplazado por un stub que redirige a `docs/aura/` (contenido corregido movido allí, no descartado silenciosamente). |
| `docs/AURA-RECOVERY-SINITRAX-01-Informe.md` (sin comitear) | Auditoría/diseño previo de la misma tarea, con hallazgos parcialmente desactualizados | Conservado sin alterar su cuerpo; se agregó una nota inicial que lo marca como superado por este informe. |
| No se encontraron `AURA_PROTOCOL v1.0/v1.1/v1.1-CA` en ningún lugar del repo. | — | — |

## F. Problemas detectados (divergencias reales vs. documentación previa)

1. `rules/AURA.md` documentaba 5 tipos de evento; el código real implementa 9
   (`REQUEST, APPROVAL, REWORK, DEPLOYMENT` no estaban documentados).
2. `rules/AURA.md` describía el rol Analista como quien "puede" abrir EXEC y aprobar etapas;
   el código realmente **restringe `REQUEST` y `APPROVAL` a `actor.type = HUMAN`** —
   `AI_ANALYST` no puede hacerlo a través de esta API hoy, sea o no un analista funcional.
3. El campo `agent` no es un enum cerrado (`ProjectAgent` existe pero solo se usa para decodificar
   filas históricas con `nAgente` numérico); las escrituras nuevas siempre van como texto libre
   en `sAgente`, sin restricción de catálogo — es una diferencia importante frente a lo que
   sugerían las notas previas.
4. No existe `WORKSTREAM` ni `PROJECT_PROGRESS` numérico en ningún nivel del modelo real.
5. Ningún endpoint de Control de Proyectos IA tiene autenticación, en ningún entorno — el borrador
   previo ya lo señalaba correctamente; se mantiene y se documenta con más detalle.
6. No existía ningún mecanismo de descubrimiento de versión de protocolo (`CURRENT_PROTOCOL`).

## G. Diseño final AURA_PROTOCOL v2.0

Ver `docs/aura/AURA_PROTOCOL_v2.md` (especificación completa), `docs/aura/AGENT_INTEGRATION.md`
(guía práctica), `docs/aura/PROTOCOL_VERSIONING.md` (versionado/descubrimiento/deprecación) y
`docs/aura/BOOTSTRAP_PROFILES.md` (perfiles mínimos, diseñados y no distribuidos).

## H. Cambios implementados

- **Código (additivo, sin cambios de esquema ni de datos):**
  `src/AURA.Application/AuraProtocol/AuraProtocolMetadataDto.cs` (DTO +
  `AuraProtocolMetadataProvider` estático) y
  `src/AURA.Api/Controllers/AuraProtocolController.cs` (`GET /api/aura-protocol`, sin
  dependencia de base de datos).
- **Tests:** `tests/AURA.IntegrationTests/AuraProtocolEndpointTests.cs` (2 casos: contrato de
  respuesta; funciona sin conectividad a SQL) y
  `tests/AURA.IntegrationTests/ProjectControl/AuraProtocolV2SmokeTest.cs` (ciclo de vida
  completo end-to-end, sección W/29).
- **Documentación:** `docs/aura/AURA_PROTOCOL_v2.md`, `docs/aura/AGENT_INTEGRATION.md`,
  `docs/aura/PROTOCOL_VERSIONING.md`, `docs/aura/BOOTSTRAP_PROFILES.md` (nuevos);
  `rules/AURA.md` (encabezado agregado, resto intacto); `docs/AURA_PROTOCOL_v2.0.md`
  (reemplazado por stub de redirección); `docs/AURA-RECOVERY-SINITRAX-01-Informe.md`
  (nota de superación agregada, cuerpo intacto).
- **No se modificó:** ningún endpoint existente, ningún DTO existente, ningún esquema de base
  de datos, `AURA.Web`, ni ningún proyecto consumidor (SiniTrax, TurnoMedix, Arterio, etc.).

## I. Descubrimiento de CURRENT_PROTOCOL

`GET /api/aura-protocol` (sin dependencia de SQL) + `docs/aura/AURA_PROTOCOL_v2.md` como única
fuente completa — ver `docs/aura/PROTOCOL_VERSIONING.md` §2 para la regla de no-divergencia
entre ambas.

## J. Roles

`ANALYST`, `CODING_AGENT`, `AUTONOMOUS_AGENT` — definidos en `docs/aura/AURA_PROTOCOL_v2.md` §3,
con la restricción real verificada de que `REQUEST`/`APPROVAL` exigen `actor.type = HUMAN`.

## K. Ciclo de vida

`REQUEST → CLAIM → PROGRESS* → HANDOFF/BLOCKER → APPROVAL → COMPLETION`, con `READ-BACK`
obligatorio tras cada escritura — ver `docs/aura/AURA_PROTOCOL_v2.md` §5, verificado end-to-end
por el smoke test (sección W).

## L. EXEC / PROJECT

Diferenciados explícitamente: `EXEC_PROGRESS` es calculado por el servidor
(`etapas cerradas / total * 100`), nunca enviado por el cliente; `PROJECT` (`ProjectStatus`) es
un estado más grueso sin porcentaje asociado hoy. Ver `docs/aura/AURA_PROTOCOL_v2.md` §2, §6.

## M. Ownership/concurrencia

Un único `CONTROL` por `(tenantId, projectCode)`; `expectedPreviousItemId` con
`UPDLOCK, HOLDLOCK` evita condiciones de carrera; `409 Conflict` fuerza reintento con el
`lastItemId` real. No existe subdivisión por `WORKSTREAM` (ver Z).

## N. Idempotencia

`POST /api/project-control/events` idempotente por `eventId` (`200 ALREADY_EXISTS` en reintento).
Cubierto por tests preexistentes (`ProjectControlEndpointTests`).

## O. READ-BACK

Obligatorio y definido explícitamente (`docs/aura/AURA_PROTOCOL_v2.md` §8): ningún éxito se
declara solo por el código HTTP; se exige releer el tablero y confirmar los valores. El smoke
test hace read-back después de cada uno de los 6 pasos del ciclo.

## P. Seguridad/autenticación

Sin autenticación en ningún entorno, hoy — condición preexistente del sistema, documentada
explícitamente como riesgo (no resuelta por esta tarea; resolverla está fuera de alcance de
"diseñar e implantar el protocolo"). Ver `docs/aura/AURA_PROTOCOL_v2.md` §11 y sección Z.

## Q. Mecanismo técnico de integración de agentes

HTTP directo contra `AURA.Api` (dev: `localhost:5235`; prod: `aura.aguarapp.com/api`). No se
introdujo CLI ni adaptador adicional — se evaluó y se descartó por desproporcionado dado que la
API ya es la interfaz estable y suficientemente simple (`docs/aura/AGENT_INTEGRATION.md`).

## R. Contingencias

`AURA_SYNCHRONIZATION = PENDING/FAILED` cuando AURA no responde, el protocolo no puede
obtenerse, o el read-back no confirma — nunca simular sincronización, nunca usar SQL directo,
nunca caer de vuelta a un protocolo pre-v2.0 como si fuera vigente (`docs/aura/AURA_PROTOCOL_v2.md` §14).

## S/T/U. Bootstrap ANALYST / CODING_AGENT / AUTONOMOUS_AGENT

Diseñados en `docs/aura/BOOTSTRAP_PROFILES.md`. **No distribuidos** — banner explícito en el
propio archivo y checklist de activación pendiente de autorización del Analista.

## V. Tests

9/9 unitarios, 57/57 de integración (incluye 2 tests nuevos del endpoint de descubrimiento y 1
smoke test de ciclo completo) — todos en verde, ninguno omitido (hay conectividad real a SQL
Server de desarrollo en este entorno).

## W. Smoke test

`AuraProtocolV2SmokeTest.FullLifecycle_AnalystToCoderToAnalyst_IsConfirmedByReadBackAtEveryStep`:
proyecto de prueba `AURAV2SMOKE` bajo el tenant seguro `900001` (fuera de rango de tenants
reales), limpiado antes y después de la corrida. Recorre
`REQUEST(HUMAN) → CLAIM(CODER) → PROGRESS(CODER) → HANDOFF(CODER→HUMAN) → APPROVAL(HUMAN) → COMPLETION(HUMAN)`
con read-back verificado en cada paso. **PASA.**

## X. Build

`dotnet build AURA.sln`: compilación correcta, 0 advertencias, 0 errores.

## Y. Git/commit

Cambios realizados pero **no comiteados** — se dejan listos para revisión del usuario/Analista
antes de comitear, conforme a la política de no comitear sin pedido explícito. Ningún cambio
ajeno fue absorbido.

## Z. Riesgos o deuda pendiente

1. **Sin autenticación** en ningún endpoint de Control de Proyectos IA, en ningún entorno —
   riesgo preexistente, no introducido ni resuelto por esta tarea.
2. **`WORKSTREAM` no implementado** — necesario para que SiniTrax represente subproyectos
   concurrentes (AFD, WhatsApp, etc.); deliberadamente fuera de alcance de esta tarea
   (ver `docs/aura/AURA_PROTOCOL_v2.md` §12). Extensión mínima recomendada si se aprueba en el
   futuro: `sWorkStream` nullable en `ProProyectoEXEC`/`ProProyectosItems`, aditivo.
3. **`AI_ANALYST` no puede `REQUEST`/`APPROVE`** a través de la API — solo `HUMAN`. Si se desea
   que un analista de IA tenga esa autoridad, es un cambio de producto explícito a decidir por
   separado, no una corrección de esta tarea.
4. **Sin `PROJECT_PROGRESS` numérico** — documentado como ausente; no se fabricó ninguno.
5. Bootstraps diseñados pero no distribuidos (intencional, ver sección 34 de la tarea).

## AA. Recomendación de despliegue

No desplegar a producción sin: (1) revisión del Analista de este informe, (2) decisión explícita
sobre activar v2.0 (`status → ACTIVE`) y distribuir los bootstraps, (3) evaluación separada de
autenticación antes de exponer los endpoints más ampliamente. El endpoint nuevo
(`GET /api/aura-protocol`) es de solo lectura, sin dependencia de datos, y de bajo riesgo — puede
publicarse junto con el resto de `AURA.Api` sin condiciones adicionales.

## AB. Estado final

```text
AURA-PROTOCOL-V2-00 EXEC = 100%
AURA_PROTOCOL v2.0 = READY (DESIGNED_PENDING_ACTIVATION — pendiente de autorización del Analista para activación/distribución)
LEGACY_PROTOCOLS = OBSOLETE
HISTORICAL_RECONCILIATION = NOT PERFORMED
```
