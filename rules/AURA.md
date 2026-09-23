```text
STATUS: LEGACY_COMPATIBILITY_POINTER
NORMATIVE OPERATIONAL PROTOCOL: AURA_PROTOCOL v2.2 (docs/aura/AURA_PROTOCOL_v2.2.md)
PROJECT MEMORY: AURA_PROJECT_MEMORY v1.0 (docs/aura/AURA_PROJECT_MEMORY_v1.0.md)
AGENT INTEGRATION CONTRACT: AURA_AGENT_CONTRACT v1.0 (docs/aura/AURA_AGENT_CONTRACT_v1.0.md)
```

> Este archivo es únicamente un puntero de compatibilidad para herramientas que todavía lo descubren. No es una
> especificación normativa y el contenido legacy inferior no prevalece ni debe interpretarse como protocolo vigente.
> Las fuentes normativas actuales son `docs/aura/AURA_PROTOCOL_v2.2.md`, `docs/aura/AURA_PROJECT_MEMORY_v1.0.md`
> y `docs/aura/AURA_AGENT_CONTRACT_v1.0.md`. La versión operativa se descubre mediante `GET /api/aura-protocol`
> y el documento indicado por `sourceDocument`, conforme a `docs/aura/PROTOCOL_VERSIONING.md`. Project Memory se
> descubre mediante `.aura/project.json` y sus documentos Core. Ante cualquier contradicción, prevalecen siempre las
> fuentes normativas vigentes; no se debe inventar estado operativo a partir de este puntero ni de su contenido legacy.

---

# Memoria permanente AURA - versión 2026-08-16.2

## Proyecto

- `projectCode`: `AURA`
- `tenantId`: `1`
- Este archivo es un puntero de compatibilidad; la fuente normativa operativa se descubre conforme a `docs/aura/PROTOCOL_VERSIONING.md`.
- La fuente del estado operativo es la API de AURA cuando el protocolo operativo la requiera.
- La fuente normativa de Project Memory es `docs/aura/AURA_PROJECT_MEMORY_v1.0.md`.

Este archivo contiene reglas compartidas, no el estado transitorio de una sesión. No guardar aquí el último actor, modelo, etapa, porcentajes o responsable actual.

## Activación obligatoria

Todo agente debe leer este archivo al iniciar o reanudar trabajo en el proyecto.

Debe actualizar AURA:

- Al comenzar una tarea significativa.
- Al entregar trabajo a otro agente o al humano.
- Al registrar progreso verificable.
- Al detectar un bloqueo.
- Al completar una etapa.
- Al completar el EXEC.

No registrar comandos internos, exploraciones menores ni acciones sin impacto funcional.

## API

- Interfaz: `http://localhost:5173/`
- API: `http://localhost:5235`
- Salud: `GET http://localhost:5235/health`
- Tablero: `GET http://localhost:5235/api/project-control?tenantId=1`
- Registro: `POST http://localhost:5235/api/project-control/events`

Usar exclusivamente la API del puerto `5235`.

No registrar mediante la interfaz web y no acceder directamente a SQL.

Nunca mostrar, copiar ni almacenar cadenas de conexión o secretos.

## Identidad

El actor es el agente que ejecuta la tarea. El IDE es solamente el entorno.

Identidades registradas:

- Devin: `CODER`, ID `2`
- ChatGPT programando: `CODER`, ID `3`
- Codex: `CODER`, ID `4`
- Grok programando: `CODER`, ID `5`
- ChatGPT analizando: `AI_ANALYST`, ID `1`
- Humano: `HUMAN`, ID `1`

VS Code, Windsurf, Cursor u otro IDE no deben enviarse como actor.

Claude Code y cualquier agente no incluido deben utilizar:

```json
{
  "type": "UNASSIGNED",
  "id": null
}
```

En ese caso, informar al usuario que falta registrar la identidad. No inventar ni reutilizar IDs.

El campo `model` debe contener el modelo real de la ejecución. Si no puede determinarse, utilizar `null` e informar la limitación. Para actores humanos utilizar `null`.

No guardar el modelo de la sesión en este archivo.

## EXEC y metodología SCHEMA

`details` representa la descripción estable del EXEC y se almacena como `sDescripcion`.

No utilizar `details` como bitácora cambiante del evento. El trabajo puntual se describe en `summary`, `nextAction`, `blocker` y `reference`.

Correspondencias:

- `details` -> `sDescripcion`
- `stage` -> `sEtapa`
- `stageProgress` -> `nPorcEtapa`
- `execProgress` -> `nPorcEXEC`

Reglas:

- `stage` debe describir la etapa actual y no superar 100 caracteres.
- Los porcentajes deben ser números entre `0` y `100`, con hasta dos decimales.
- Un valor legado `null` significa "Sin informar"; no debe interpretarse como cero.
- Cada evento debe repetir la fotografía completa vigente del EXEC: descripción, etapa y porcentajes.
- No dejar esos campos en `null` después de establecer la línea base.

## Responsabilidades

Solamente un agente Analista puede:

- Iniciar un EXEC nuevo.
- Definir o modificar el plan aprobado de etapas.
- Establecer la descripción estable del EXEC.
- Inicializar `execProgress` en `0`.
- Seleccionar la primera etapa.
- Inicializar `stageProgress` en `0`.
- Validar y cerrar una etapa al `100%`.
- Recalcular el EXEC cuando cambie el número aprobado de etapas.

El agente Codificador puede:

- Trabajar sobre la etapa vigente.
- Actualizar `stageProgress` según tareas objetivamente terminadas y verificadas.
- Registrar progreso, bloqueo o entrega.
- Entregar la etapa al Analista para validación.

El Codificador no puede crear otro EXEC, cambiar unilateralmente el plan de etapas ni declarar cerrada una etapa sin validación.

## Cálculo de avance

El avance de etapa se calcula sobre tareas o criterios previamente definidos y verificables.

No asignar porcentajes arbitrarios. Si no existe una base medible, el Analista debe definirla antes.

Una etapa llega a `100%` solamente cuando cumple sus criterios y el Analista la cierra.

Registrar primero el cierre de la etapa actual al `100%`. Después registrar el inicio de la etapa siguiente al `0%`.

El avance del EXEC se calcula exclusivamente con etapas cerradas:

`execProgress = etapas cerradas / total de etapas aprobadas * 100`

Redondear a dos decimales.

Ejemplo: cinco etapas cerradas de diez equivalen a `50%`.

El avance parcial de la etapa actual no incrementa `execProgress`.

Si cambia el total de etapas, el Analista debe registrar la modificación del plan y recalcular el porcentaje.

Un bloqueo no cambia automáticamente los porcentajes. Solo se modifican cuando existe nueva evidencia de avance.

El EXEC llega a `100%` únicamente cuando todas las etapas están cerradas.

## Flujo de eventos

Antes de registrar:

1. Consultar el tablero.
2. Localizar `AURA`.
3. Obtener su `lastItemId`.
4. Generar un UUID nuevo como `eventId`.
5. Conservar la fotografía completa del EXEC.
6. Validar el contrato de escritura.

Eventos:

- `CLAIM`: comienzo o toma de control.
- `PROGRESS`: avance y continuidad del mismo responsable.
- `HANDOFF`: entrega a otro agente o al humano.
- `BLOCKER`: imposibilidad real de continuar.
- `COMPLETION`: EXEC completamente finalizado.

Estados:

- `IN_PROGRESS`
- `BLOCKED`
- `COMPLETED`

Flujo habitual:

- Analista inicia EXEC y entrega etapa al Codificador.
- Codificador implementa y entrega al Analista.
- Analista valida, cierra etapa y abre la siguiente.
- `COMPLETION` solo se utiliza con todas las etapas cerradas y `execProgress = 100`.

Cuando no exista un sucesor automatizado confirmado, entregar el control a `HUMAN`, ID `1`.

## Validación obligatoria del contrato

Antes del POST, inspeccionar el OpenAPI publicado por la API y confirmar que el cuerpo de `POST /api/project-control/events` admite:

- `stage`
- `stageProgress`
- `execProgress`

No basta con que esos campos aparezcan en la respuesta del GET.

Una respuesta `201` tampoco demuestra persistencia si la API ignora propiedades desconocidas.

Si el POST todavía no admite los tres campos:

- No simular que el avance fue registrado.
- No utilizar SQL como alternativa.
- Finalizar el trabajo local y responder:
  `AURA pendiente: AURA. Motivo: contrato de eventos sin soporte para avance EXEC/etapa.`

## Contrato esperado

```json
{
  "tenantId": 1,
  "projectCode": "AURA",
  "eventId": "<UUID>",
  "expectedPreviousItemId": 0,
  "eventType": "CLAIM | PROGRESS | HANDOFF | BLOCKER | COMPLETION",
  "actor": {
    "type": "CODER | AI_ANALYST | HUMAN | UNASSIGNED",
    "id": "ID o null"
  },
  "model": "modelo real o null",
  "status": "IN_PROGRESS | BLOCKED | COMPLETED",
  "summary": "resumen del evento, máximo 300 caracteres",
  "details": "descripción estable del EXEC",
  "stage": "descripción de la etapa actual",
  "stageProgress": 0,
  "execProgress": 0,
  "nextControl": {
    "type": "CODER | AI_ANALYST | HUMAN | UNASSIGNED",
    "id": "ID o null"
  },
  "nextAction": "próxima acción o null",
  "blocker": "bloqueo o null",
  "reference": "commit, tarea, rama, PR o archivo"
}
```

`expectedPreviousItemId` debe contener el último ID real o `null` cuando todavía no exista uno.

## Respuestas y concurrencia

- `201 CREATED`: registrado.
- `200 ALREADY_EXISTS`: registrado previamente; tratar como éxito.
- `409 CONFLICT`: consultar nuevamente y reintentar con el nuevo `lastItemId`, conservando el mismo `eventId`.
- `400`: corregir el contrato.
- `404`: revisar el `projectCode`.
- API inaccesible: no usar SQL; informar AURA pendiente.

Después de un POST exitoso, consultar nuevamente el tablero y comprobar:

- El nuevo `lastItemId`.
- El evento registrado.
- La descripción del EXEC.
- La etapa.
- Ambos porcentajes.
- El modelo y el siguiente responsable.

No declarar éxito si la lectura posterior no confirma la persistencia.

## Respuesta obligatoria

Éxito:

`AURA actualizado: AURA, <EVENT_TYPE>, itemId <ID>, etapa <ETAPA> <PORC_ETAPA>%, EXEC <PORC_EXEC>%, modelo <MODELO>, control <RESPONSABLE>.`

Fallo:

`AURA pendiente: AURA. Motivo: <ERROR>.`
