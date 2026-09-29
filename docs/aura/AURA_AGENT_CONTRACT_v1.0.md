# AURA_AGENT_CONTRACT v1.0

**Tipo:** especificación de integración neutra para agentes de IA  
**Estado:** STABLE — vinculado a `AURA_PROJECT_MEMORY v1.0`  
**Alcance:** cualquier agente, modelo, IDE o entorno que acceda a un proyecto con `AURA_PROJECT_MEMORY`  
**Dependencias:** `AURA_PROJECT_MEMORY v1.0`; `AURA_PROTOCOL` para autoridad operacional (opcional cuando no hay red)  

> Este contrato no reemplaza `AURA_PROJECT_MEMORY v1.0`. Define cómo un agente *descubre*, *lee*, *actualiza* y *cierra sesiones* sobre Project Memory sin acoplarse a un proveedor, modelo, IDE o sistema operativo concreto. La autoridad operacional (leases, eventos, autenticación) permanece en `AURA_PROTOCOL`.

---

## 1. Propósito

`AURA_AGENT_CONTRACT` permite que un agente que entra en un proyecto sepa, con una sola operación (`aura agent bootstrap`), qué debe hacer para trabajar correctamente: leer el Core, determinar si una tarea es relevante, abrir una sesión, actualizar documentación y cerrar la sesión. Elimina la necesidad de que el usuario recuerde manualmente cada paso.

## 2. Relación con Project Memory y AURA Protocol

```text
AURA_AGENT_CONTRACT  =  cómo un agente interactúa con Project Memory
AURA_PROJECT_MEMORY  =  qué conocimiento se guarda en el proyecto
AURA_PROTOCOL        =  autoridad operacional, autenticación, leases, eventos
```

Project Memory y este contrato funcionan **sin API**, **sin red** y **sin Git**. `AURA_PROTOCOL` se consulta cuando está disponible y cuando el rol lo requiera, pero nunca se infiere de Project Memory. Cuando el objetivo sea solamente registrar evolución durable, el canal compatible es `REGISTRATION_ONLY` de `AURA Agent Registration Protocol v2.3` con credencial técnica `INGESTION` asociada al proyecto activo; las credenciales legacy `LEGACY_TENANT_WIDE` se mantienen solo durante transición. Ese registro no otorga ni presume autoridad de `GOVERNED_EXECUTION`.

## 3. Descubrimiento

Un agente compatible debe:

1. Resolver `PROJECT_ROOT` buscando `.aura/project.json` desde el directorio de trabajo hacia arriba.
2. Si no existe, no hay Project Memory en esta rama del sistema de archivos; el agente puede continuar sin ella.
3. Si existe, ejecutar `aura agent bootstrap` (o su equivalente programático) en `PROJECT_ROOT`.
4. `AGENTS.md` y `AURA.md` son *adaptadores de descubrimiento*, no normativa. El contrato central es este documento; la fuente de identidad es `.aura/project.json`.

## 4. Bootstrap result

El bootstrap devuelve un contexto estructurado con:

- identidad del proyecto (`projectId`, `projectName`, `projectRoot`, `valid`);
- versión de Project Memory y documentos Core a leer;
- módulos opcionales presentes o habilitados;
- identidad de máquina (si existe);
- contexto Git (si existe);
- sesiones abiertas en la máquina local;
- políticas del contrato;
- comandos necesarios para `start`, `status` y `close`;
- diagnósticos (`ERROR`, `WARNING`, `INFO`).

No devuelve el contenido completo de documentos, código ni historial. El agente carga progresivamente.

## 5. Trabajo relevante

Una sesión es **relevante** si produce o valida conocimiento con impacto de continuidad. Ejemplos no exhaustivos:

- cambio de código, configuración, arquitectura o dependencias;
- decisión funcional o técnica;
- análisis sustancial con conclusión;
- nuevo bloqueo o resolución de uno;
- cambio relevante de backlog;
- pruebas significativas o hallazgo de deuda técnica.

**No** es relevante:

- consulta trivial sin modificación persistente;
- pregunta breve sin impacto;
- lectura exploratoria sin conclusión;
- comentario o explicación que no altera archivos del proyecto.

### Política recomendada

- Ejecutar `bootstrap` siempre que se va a trabajar sobre el proyecto.
- Abrir sesión (`session start`) **antes del primer cambio persistente** si la tarea se determina relevante.
- No abrir sesiones triviales.

## 6. Ciclo de vida del agente

```text
AGENTE ENTRA
     ↓
DESCUBRE AURA  (bootstrap)
     ↓
CARGA PROJECT MEMORY  (Core + módulos necesarios)
     ↓
DETERMINA SI LA TAREA ES RELEVANTE
     ↓
ABRE SESIÓN  (session start)
     ↓
TRABAJA  (actualiza código y documentos según contrato)
     ↓
ACTUALIZA MEMORIA DEL PROYECTO  (estado, decisiones, backlog)
     ↓
CIERRA SESIÓN  (session close)
```

El bootstrap no abre sesión automáticamente. La apertura es una decisión del agente basada en la relevancia de la tarea.

## 7. Actualización documental

El agente decide qué documento actualizar según el tipo de cambio. La asignación es un contrato, no una regla rígida:

| Cambio | Documento sugerido |
|---|---|
| Estado operativo general del proyecto | `docs/01-ESTADO-ACTUAL.md` |
| Decisión importante | `docs/02-DECISIONES.md` o módulo de decisiones habilitado |
| Nuevo pendiente o cambio en backlog | `docs/03-BACKLOG.md` |
| Cambio estructural/arquitectura | `docs/04-ARQUITECTURA.md` |
| Resumen de sesión cerrada | `docs/historial/<timestamp>__<sessionId>.md` |

Los módulos opcionales solo se actualizan si existen o han sido habilitados explícitamente. El agente no crea módulos opcionales automáticamente para registrar un cambio menor.

## 8. Pre-close compliance

Antes de `session close`, el agente debe verificar hechos comprobables:

1. Existe una sesión activa con `sessionId` válido.
2. `projectId` de la sesión coincide con el proyecto actual.
3. Project Memory Core es válido (`.aura/project.json`, `AURA.md`, `docs/00-PROYECTO.md`, `docs/01-ESTADO-ACTUAL.md`, `docs/historial/`).
4. `docs/01-ESTADO-ACTUAL.md` no contiene marcadores de conflicto Git (`<<<<<<<`, `=======`, `>>>>>>>`).
5. Opcionalmente: documentos actualizados durante la sesión se declaran en `updatedDocuments[]`.

No bloquear por inferencias semánticas ("el resumen parece viejo"). Solo hechos objetivos.

## 9. Handoff entre agentes

Varias sesiones pueden coexistir en el mismo proyecto. Una sesión abierta es **local a la máquina**; la sesión cerrada **viaja con el proyecto**. Un agente puede continuar una sesión iniciada por otro si conserva el mismo `sessionId` y objetivo lógico; al cerrar, el registro histórico refleja el agente/modelo actual de cierre. No se implementa locking; la concurrencia real se maneja mediante Git y documentación.

## 10. Comandos obligatorios

Un entorno compatible debe poder ejecutar:

```text
aura agent bootstrap  [--path <dir>] [--machine-path <file>] [--json]
aura project session start  --objective <text> --role <role> [--path <dir>] [--machine-path <file>]
aura project session status  [--path <dir>] [--json]
aura project session close  --session <uuid> --input <file> [--path <dir>] [--json]
aura project validate  [<path>] [--json]
```

## 11. Offline y sin Git

El contrato funciona sin red, sin API AURA y sin Git. Los campos Git son opcionales y se omiten sin error. La ausencia de `machine.json` es `INFO`, no `ERROR`.

## 12. Seguridad y privacidad

El bootstrap y el histórico no contienen secretos, tokens, credenciales, remote URLs, `user.name`/`user.email` de Git, ni variables de entorno. El agente no debe escribir estos valores en Project Memory.

## 13. Versionado

`AURA_AGENT_CONTRACT v1.0` está acoplado a `AURA_PROJECT_MEMORY v1.0`. Cambios mayores requerirán `v2.0`; cambios compatibles incrementarán la versión menor/patch y se documentarán sin modificar `AURA_PROJECT_MEMORY`.

## 14. Adaptadores por proveedor

`AGENTS.md` es un adaptador genérico. Pueden existir adaptadores concretos (`CLAUDE.md` para Claude, instrucciones Devin, etc.) que referencien este contrato. Ningún adaptador reemplaza el contrato universal.
