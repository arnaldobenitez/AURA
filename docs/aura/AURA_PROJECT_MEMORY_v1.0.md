# AURA_PROJECT_MEMORY v1.0

**Tipo:** especificación normativa transversal
**Estado:** STABLE — integración normativa completada en Etapa 3; implementación aún no realizada
**Alcance:** cualquier proyecto que adopte AURA
**Dependencias:** ninguna dependencia de lenguaje, framework, sistema operativo, IDE, agente, modelo, proveedor, repositorio o usuario concretos

> Esta especificación define la memoria documental portable de un proyecto AURA. No reemplaza ni duplica `AURA_PROTOCOL` ni los protocolos de rol. Define qué conocimiento persistir dentro del proyecto y cómo debe ser consumido por ellos.

---

## 1. Propósito

AURA Project Memory conserva dentro de `PROJECT_ROOT` el conocimiento operativo necesario para comprender y continuar un proyecto cuando cambian las sesiones, computadoras, rutas, agentes, modelos o herramientas.

Los chats, historiales de herramientas y sistemas externos pueden complementar esta memoria, pero nunca deben ser la única fuente para reconstruir el estado del proyecto.

El protocolo privilegia una memoria pequeña, legible y verificable: se persiste el contexto que cambia decisiones o continuidad, no el transcript completo ni cada actividad trivial.

## 2. Alcance y límites

Incluye:

- identidad portable del proyecto;
- separación entre identidad del proyecto, máquina, usuario, agente y sesión;
- documentos mínimos y módulos documentales opcionales;
- ciclo de apertura, trabajo y cierre de una sesión;
- estado consolidado actual e historial append-only;
- operación multi-PC, multi-agente, con o sin Git;
- privacidad, versionado, consistencia y criterios de conformidad.

No incluye:

- CLI, UI, API, base de datos, watcher, sincronización o locking distribuido;
- comandos de inicialización o validación;
- automatización de registro de sesiones;
- reglas de negocio del proyecto consumidor;
- sustitución de `AURA_PROTOCOL` o de `AURA_PROTOCOL_CODING_AGENT`;
- una implementación dependiente de una plataforma o proveedor.

## 3. Principios normativos

1. **El protocolo es transversal.** El proyecto implementa la estructura, pero no redefine su semántica.
2. **Portabilidad por diseño.** Las referencias a contenido interno son relativas a `PROJECT_ROOT`; nunca identifican el proyecto mediante una ruta física.
3. **Identidad estable.** `projectId` viaja dentro del proyecto y no depende de carpeta, nombre visible, máquina ni Git.
4. **Estado consolidado separado de hechos históricos.** El estado actual es mutable; el historial es append-only.
5. **Carga progresiva.** Se lee primero el mínimo necesario y se profundiza según la tarea.
6. **Trazabilidad proporcional.** Solo se documentan decisiones, cambios, hallazgos y bloqueos que afecten continuidad o conocimiento del proyecto.
7. **No sobrescritura histórica.** Una sesión nunca modifica ni reutiliza el registro de otra sesión.
8. **Privacidad por minimización.** Nunca se persisten secretos ni datos sensibles innecesarios.
9. **Evidencia antes que afirmación.** El estado debe reflejar cambios verificables, pruebas ejecutadas y limitaciones conocidas.
10. **Compatibilidad abierta.** Los campos desconocidos deben poder ignorarse de forma segura y los cambios incompatibles requieren versión mayor.

## 4. Terminología

- **AURA Project Memory:** protocolo definido en este documento.
- **PROJECT_ROOT:** raíz lógica del proyecto AURA en una instalación concreta.
- **Core:** conjunto mínimo de archivos exigido por este protocolo.
- **Módulo:** conjunto opcional de documentos con una responsabilidad específica.
- **Estado actual:** representación breve y consolidada de la situación vigente.
- **Historial:** registros inmutables de sesiones relevantes.
- **Sesión relevante:** interacción que produce o valida conocimiento con impacto de continuidad, decisión, estado, código, configuración, pruebas, bloqueo o entrega.
- **Copia/movimiento:** nueva ubicación del mismo proyecto, conserva identidad.
- **Fork:** nuevo proyecto derivado deliberadamente, obtiene identidad nueva.

## 5. Identidades

### 5.1 Identidad del proyecto

`projectId` es el identificador persistente y primario del proyecto. Debe ser un UUID v4 en representación textual canónica, en minúsculas y con guiones. Es globalmente único, no reutilizable y estable.

Reglas:

- se genera una sola vez al crear o adoptar formalmente un proyecto;
- se persiste en `.aura/project.json`;
- no se deriva de ruta, nombre de carpeta, nombre visible, usuario, máquina, tenant, branch o remoto Git;
- no cambia al mover o renombrar la carpeta, cambiar de computadora, clonar el repositorio o copiar el proyecto;
- una copia normal representa el mismo proyecto y conserva `projectId`;
- un fork intencional debe ejecutar en el futuro un procedimiento explícito de derivación/regeneración, que produzca otro `projectId` y deje constancia de su origen;
- no se debe “reparar” una identidad duplicada cambiando el ID silenciosamente: se marca la inconsistencia y se resuelve mediante operación explícita.

`projectName` es un nombre visible editable e informativo. Nunca es identidad.

### 5.2 Identidad local de máquina

AURA puede mantener fuera del proyecto una identidad local, conceptualmente bajo el directorio de configuración del usuario de cada sistema operativo (por ejemplo, el equivalente de `%USERPROFILE%/.aura/`). Esta ubicación no forma parte del estándar portable ni debe codificarse como ruta universal.

- `machineId`: identificador local persistente, generado una vez por instalación o perfil local; debe ser un UUID v4. No pertenece a la identidad del proyecto y no se copia como parte del proyecto.
- `machineName`: nombre informativo actual del equipo o entorno. Puede cambiar sin modificar `machineId` ni `projectId`.

Si no existe identidad local, una sesión puede registrar `machineId: null` y `machineName: null` o indicar que la identidad no está disponible. No se debe inventar una identidad estable a partir de la ruta o el nombre del equipo.

### 5.3 Identidad del usuario

Cada sesión debe distinguir:

- `logicalUser`: persona, equipo lógico o identidad responsable de la sesión; puede ser un nombre o identificador configurado.
- `osUser`: cuenta del sistema operativo que ejecutó el proceso.

Pueden ser diferentes, nulos o no aplicables. Un mismo `logicalUser` puede trabajar desde varias máquinas; varias personas pueden compartir una máquina; un proceso automático puede usar `logicalUser: null` o una identidad de servicio claramente declarada. Ninguno de estos valores identifica el proyecto.

### 5.4 Identidad del agente y rol

Una sesión relevante registra, cuando sea conocido:

- `agent`: nombre del agente o herramienta que ejecutó el trabajo;
- `model`: modelo real utilizado, sin inferirlo; `null` si no puede determinarse;
- `auraRole`: rol AURA desempeñado en la sesión, extensible y no limitado a un proveedor.

El IDE es entorno, no identidad del agente. `agent` y `model` son metadatos de trazabilidad y no autorización. La autoridad, eventos, autenticación y restricciones de un rol pertenecen al protocolo AURA correspondiente.

### 5.5 Identidad de sesión

`sessionId` identifica una ejecución relevante, no al proyecto ni a la máquina. Debe ser un UUID v4 en representación textual canónica, en minúsculas y con guiones. Debe generarse al comenzar la sesión relevante, ser único globalmente dentro del historial y no reutilizarse.

La unicidad no depende del reloj, la máquina o el agente. El nombre de archivo incorpora el `sessionId`; si aun así existe colisión, se conserva el registro original y se agrega un sufijo técnico único, sin sobrescribir.

## 6. PROJECT_ROOT

`PROJECT_ROOT` es la raíz lógica que contiene la manifestación AURA del proyecto:

```text
.aura/project.json
```

La presencia de ese archivo es el criterio primario de reconocimiento. `AURA.md` es el punto de entrada humano/agente y debe acompañarlo en una instalación conforme, pero no reemplaza la manifestación machine-readable.

Reglas de resolución conceptual:

1. Desde el directorio de trabajo, se buscan ancestros hasta encontrar `.aura/project.json` válido.
2. Si se encuentran varios candidatos, prevalece el candidato más cercano que forme una pareja válida con el `project.json` y su `projectId`; no se atraviesa arbitrariamente el límite de un proyecto contenedor.
3. La ejecución desde un subdirectorio debe resolver el mismo `PROJECT_ROOT` sin depender del directorio actual.
4. Un repositorio Git no es necesariamente `PROJECT_ROOT`; Git es opcional y auxiliar.
5. Un repositorio puede contener varios proyectos AURA: cada proyecto tiene su propia raíz lógica y manifestación. La herramienta debe requerir desambiguación si el contexto no determina cuál corresponde.
6. En un monorepo, la raíz Git puede contener varios `PROJECT_ROOT`; no se debe elevar automáticamente la raíz lógica al nivel Git.
7. Un proyecto sin Git es plenamente válido.
8. `AURA.md` sin `.aura/project.json` indica instalación incompleta o legado; no permite inventar un `projectId` automáticamente en esta etapa.
9. Ninguna ruta absoluta es válida como referencia de identidad o como vínculo normativo a otro archivo interno. Las rutas documentales se expresan relativas a `PROJECT_ROOT` y con separador `/` en documentación portable.

El algoritmo concreto de detección queda fuera de alcance de v1.0.

## 7. Estructura documental

La estructura recomendada es:

```text
PROJECT_ROOT/
├── AURA.md
├── AGENTS.md                         # opcional, descubrimiento
├── .aura/
│   ├── project.json                  # Core
│   └── session-template.md           # opcional, plantilla
└── docs/
    ├── 00-PROYECTO.md                # Core
    ├── 01-ESTADO-ACTUAL.md           # Core
    ├── historial/                     # Core
    ├── 02-DECISIONES.md               # módulo DECISIONES
    ├── 03-BACKLOG.md                  # módulo BACKLOG
    ├── 04-ARQUITECTURA.md             # módulo ARQUITECTURA
    ├── decisiones/                    # módulo ADR
    └── <módulos adicionales>/
```

El nombre y ubicación de los documentos Core son normativos en esta versión. Un proyecto puede añadir módulos sin crear todos los módulos listados. No debe crearse documentación vacía solo para completar una plantilla.

### 7.1 Core obligatorio

Un proyecto conforme contiene:

- `AURA.md`;
- `.aura/project.json`;
- `docs/00-PROYECTO.md`;
- `docs/01-ESTADO-ACTUAL.md`;
- `docs/historial/`.

La carpeta de historial puede estar vacía al inicio, pero debe existir antes del primer registro relevante. Un proyecto recién inicializado puede tener documentos breves, siempre que sean verdaderos y suficientes para su etapa.

### 7.2 Módulos opcionales

Son módulos estándar, no obligaciones:

- `02-DECISIONES.md` y `docs/decisiones/` (DECISIONES/ADR);
- `03-BACKLOG.md` (BACKLOG);
- `04-ARQUITECTURA.md` (ARQUITECTURA);
- testing, deploy, incidentes, seguridad, operación u otros módulos que el proyecto necesite.

Un módulo externo de gestión puede ser la fuente operativa del backlog o de incidentes. El documento AURA debe contener solo el resumen necesario y una referencia portable o un identificador externo no sensible; no debe duplicar el sistema entero.

## 8. Responsabilidad de los documentos

### 8.1 `AURA.md`

Punto de entrada humano/agente. Debe indicar que el proyecto adopta AURA Project Memory, cómo localizar `PROJECT_ROOT`, qué Core cargar primero y qué protocolos AURA externos deben consultarse. Puede incluir reglas locales mínimas y enlaces relativos.

No debe contener el protocolo completo, el historial, el estado transitorio de una sesión, credenciales, secretos, rutas absolutas, ni una copia de `CLAUDE.md` u otro archivo de agente.

### 8.2 `.aura/project.json`

Manifest machine-readable portable. Schema conceptual v1:

```json
{
  "schemaVersion": "1.0",
  "protocol": "AURA_PROJECT_MEMORY",
  "protocolVersion": "1.0",
  "projectId": "<UUID>",
  "projectName": "<nombre visible>",
  "createdAt": "<ISO-8601 con offset o UTC>",
  "status": "<opcional, definido por el proyecto>",
  "defaultDocuments": {
    "overview": "docs/00-PROYECTO.md",
    "currentState": "docs/01-ESTADO-ACTUAL.md",
    "history": "docs/historial/"
  },
  "modules": ["DECISIONES", "BACKLOG", "ARQUITECTURA"]
}
```

`schemaVersion`, `protocol`, `protocolVersion` y `projectId` son obligatorios. `projectName`, `createdAt`, `status`, `defaultDocuments` y `modules` son recomendados; las extensiones deben ser aditivas y no alterar el significado de los campos normativos.

No debe contener `machineId`, `machineName`, `localProjectPath`, `osUser`, credenciales, tokens, secretos, ni rutas absolutas. El estado operativo detallado no pertenece al manifest.

### 8.3 `docs/00-PROYECTO.md`

Describe qué es el proyecto con información relativamente estable: propósito, alcance, usuarios, objetivos, restricciones relevantes y exclusiones. Puede incluir enlaces relativos a módulos.

No debe funcionar como bitácora, backlog exhaustivo, registro de sesiones, lista de cambios diaria ni estado transitorio.

### 8.4 `docs/01-ESTADO-ACTUAL.md`

Responde “¿dónde está parado el proyecto ahora?”. Debe ser breve y consolidado. Secciones mínimas recomendadas:

1. última actualización y responsable lógico;
2. resumen de situación;
3. objetivo o etapa vigente;
4. trabajo completado verificable;
5. trabajo en curso;
6. bloqueos, riesgos y supuestos;
7. decisiones vigentes relevantes;
8. pendientes y próximo paso recomendado;
9. pruebas/validaciones y limitaciones conocidas;
10. referencias relativas a módulos o artefactos.

No debe contener el transcript de una sesión ni hechos históricos sin consolidar. Si una sección no aplica, se indica brevemente en vez de inventar contenido.

### 8.5 `docs/02-DECISIONES.md`

Resumen de decisiones relevantes vigentes, con fecha, contexto breve, decisión, consecuencias y estado. No es un historial exhaustivo. Una decisión que afecte arquitectura, seguridad, datos, interfaces públicas, portabilidad o una restricción difícil de revertir merece un ADR separado.

### 8.6 `docs/03-BACKLOG.md`

Lista resumida de trabajo pendiente solo cuando el proyecto adopta este módulo. Cada ítem debe tener identificador, descripción breve, prioridad/estado si aplica y referencia externa relativa o estable. No duplica un gestor externo ni se presenta como fuente más actual si el gestor es normativo.

### 8.7 `docs/04-ARQUITECTURA.md`

Descripción de la arquitectura vigente: límites, componentes, dependencias, flujos y restricciones. Los cambios históricos pertenecen a ADR o historial; este archivo se actualiza para representar la arquitectura actual.

### 8.8 ADR

Se crea un ADR cuando una decisión es significativa, difícil de revertir, transversal, de seguridad/datos/arquitectura, o cuando futuros agentes necesitarían conocer el razonamiento para no reabrirla. Un ADR contiene, como mínimo: identificador único, título, fecha, estado, contexto, decisión, alternativas consideradas, consecuencias y referencias relativas. Los ADR no se reescriben para borrar historia; se reemplazan mediante un nuevo ADR que referencia al anterior.

### 8.9 `docs/historial/`

Contiene registros semánticos de sesiones relevantes. Es append-only a nivel lógico: no se edita ni sobrescribe un registro publicado. Una corrección se agrega como enmienda o nuevo registro que referencia el anterior.

## 9. Ciclo de vida de sesión

### 9.1 Apertura

Antes de una tarea relevante, un agente compatible debe:

1. resolver `PROJECT_ROOT`;
2. leer `AURA.md`;
3. leer y validar conceptualmente `.aura/project.json`;
4. leer `docs/00-PROYECTO.md`;
5. leer `docs/01-ESTADO-ACTUAL.md`;
6. cargar solo los módulos necesarios para la tarea;
7. consultar el protocolo AURA operativo y su mecanismo de estado cuando la tarea lo requiera;
8. crear `sessionId` y capturar metadatos disponibles.

Una consulta simple puede limitarse al contexto necesario y no requiere historial. La carga progresiva no autoriza a omitir el Core cuando se va a modificar o consolidar estado.

Si AURA o una fuente operativa externa no está disponible, el agente no inventa estado sincronizado: continúa solo si puede hacerlo de forma segura y deja la limitación en el cierre.

### 9.2 Durante el trabajo

El agente debe persistir o preparar para persistir, según el workflow, lo siguiente:

- cambios que alteren el estado o el alcance;
- decisiones y su fundamento;
- bloqueos reales y su impacto;
- hallazgos que eviten repetir investigación;
- desviaciones del alcance y deuda técnica creada;
- pruebas ejecutadas, resultados y limitaciones;
- cambios arquitectónicos.

No es obligatorio escribir en cada paso. Los hechos se consolidan en puntos verificables. Las reglas de autoridad, control, eventos, handoff y comunicación de cada rol siguen perteneciendo al protocolo AURA correspondiente.

### 9.3 Cierre

Una sesión es relevante si produjo o validó una implementación, cambio documental/configuracional, decisión significativa, análisis sustancial, bloqueo, cambio arquitectónico, prueba con resultado importante, entrega o cambio que otro actor necesita conocer.

Una pregunta aislada, lectura sin hallazgo, exploración menor o conversación sin impacto no requiere historial.

En una sesión relevante se evalúa, en este orden:

1. actualizar `01-ESTADO-ACTUAL.md` con la verdad consolidada;
2. actualizar backlog si se adoptó el módulo y cambió trabajo pendiente;
3. actualizar resumen de decisiones y crear ADR si corresponde;
4. actualizar arquitectura si cambió la arquitectura vigente;
5. crear un registro nuevo en `docs/historial/`;
6. verificar que no se haya incluido información sensible;
7. informar limitaciones, conflictos o sincronización pendiente.

El historial debe registrar incluso un trabajo fallido si cambió el conocimiento o dejó un bloqueo relevante. No se registra transcript completo automáticamente.

## 10. Registro semántico de sesión

Cada registro debe ser Markdown legible y contener los campos obligatorios:

- `sessionId`;
- `projectId`;
- `startedAt`;
- `finishedAt` o estado `in_progress` si el registro provisional es necesario;
- `auraRole`;
- `objective`;
- `result`;
- `pending`;
- `nextRecommendedStep`.

Campos recomendados cuando estén disponibles:

- `machineId`, `machineName`;
- `logicalUser`, `osUser`;
- `agent`, `model`;
- `localProjectPath`;
- `gitBranch`, `initialCommit`, `finalCommit`, `workingTreeInitial`, `workingTreeFinal`;
- `modifiedFiles` como rutas relativas;
- `decisions`, `problems`, `tests`, `references`.

Los campos no disponibles se omiten o se expresan como `null`/`unknown` según la plantilla; no se adivinan. `localProjectPath` es diagnóstico efímero y opcional: nunca se usa como identidad ni como referencia normativa.

Se recomienda esta forma:

```markdown
# Sesión <sessionId>

| Campo | Valor |
|---|---|
| projectId | `<UUID>` |
| sessionId | `<UUID>` |
| startedAt | `2026-09-03T23:35:12-03:00` |
| finishedAt | `2026-09-03T23:58:04-03:00` |
| auraRole | `...` |
| agent | `...` |
| model | `...` |

## Objetivo
## Resultado
## Cambios
## Decisiones
## Problemas y bloqueos
## Pruebas y validaciones
## Pendientes
## Próximo paso recomendado
```

`modifiedFiles` y referencias internas usan rutas relativas. El contenido debe resumir semánticamente qué ocurrió, no reproducir mensajes, prompts ni transcript.

## 11. Convención de nombres y tiempo

Nombre recomendado:

```text
YYYY-MM-DDTHH-mm-ssZ__<sessionId>__<slug>.md
```

- el prefijo se expresa en UTC (`Z`) para ordenar y evitar ambigüedad entre PCs;
- los timestamps dentro del documento usan ISO 8601 con offset real cuando se conoce, o UTC (`Z`);
- `sessionId` garantiza unicidad y no depende del nombre de máquina, agente o modelo;
- `slug` es opcional, corto, descriptivo, en minúsculas, normalizado a ASCII cuando sea posible, con `[a-z0-9-]` únicamente;
- se eliminan separadores, saltos de línea, caracteres de control y datos sensibles;
- la longitud del slug se limita para mantener nombres portables (recomendación: 60 caracteres);
- si dos nombres siguen colisionando, se agrega `-2`, `-3`, etc. o un fragmento del `sessionId`, sin reemplazar el archivo existente.

La hora del nombre no sustituye los timestamps del contenido. El reloj local no se trata como autoridad absoluta: si hay una discrepancia evidente, se conserva la información y se documenta.

## 12. Estado actual e historial

`01-ESTADO-ACTUAL.md` es la fuente documental primaria del estado consolidado vigente. El historial es evidencia de hechos y contexto, no una vista que se reescribe.

Ante contradicciones:

1. no se borra ni modifica el registro histórico original;
2. se marca la contradicción en el estado actual o en una enmienda;
3. se reconstruye comparando registros, archivos y, si existe, evidencia Git o del sistema externo;
4. se actualiza el estado solo después de consolidar la resolución;
5. si no puede resolverse, el estado declara explícitamente la incertidumbre y el próximo paso.

Un registro histórico más nuevo no invalida automáticamente un estado actual que no fue consolidado, y un estado actual no convierte en falso un hecho histórico. La prioridad operativa es el estado actual validado; la prioridad de trazabilidad es el historial inmutable. Los protocolos AURA externos pueden aportar una fuente operativa adicional, pero no deben ser sustituidos por una inferencia del historial.

## 13. Multi-PC y copia

Al copiar o mover el proyecto completo, `.aura/project.json`, Core, módulos e historial viajan juntos y `projectId` permanece idéntico. La nueva sesión puede registrar otro `machineId`, `machineName` y `localProjectPath` sin que eso sea un cambio de proyecto.

La identidad local de máquina no se versiona como archivo del proyecto. La ruta local puede omitirse por privacidad. No se exige Git, remoto, red ni servicio externo para leer o continuar la memoria documental.

## 14. Concurrencia

Esta versión no define locking distribuido. Cuando dos agentes o máquinas trabajan simultáneamente:

- cada sesión usa un `sessionId` distinto y escribe un historial distinto;
- los documentos consolidados pueden entrar en conflicto y el mecanismo de control de versiones del proyecto, si existe, debe detectarlo;
- nunca se resuelve una colisión sobrescribiendo silenciosamente el trabajo ajeno;
- se conserva toda la información de ambas versiones, se comparan y se reconcilian explícitamente;
- tras la conciliación, se actualiza el estado actual y se agrega un registro que documenta la resolución;
- si un documento queda corrupto o ambiguo, el estado debe marcarse como `INCONSISTENT` o equivalente local hasta resolverlo;
- la ausencia de Git no elimina la obligación de no pérdida: se preservan copias/versiones por el mecanismo disponible, fuera del estándar de implementación.

`docs/historial/` reduce colisiones por diseño, pero no resuelve por sí solo la concurrencia de `01-ESTADO-ACTUAL.md`, decisiones o arquitectura.

## 15. Git opcional

AURA Project Memory funciona sin Git. Si Git está presente, es evidencia auxiliar y puede registrar branch, commit inicial/final y estado dirty/clean. Los commits no son obligatorios, no siempre existen y no se ejecutan automáticamente por este protocolo.

- `initialCommit` y `finalCommit` son opcionales y pueden ser `null`;
- se registra la rama observada, no se presupone una rama principal;
- se registra `workingTreeInitial` y `workingTreeFinal` como `clean`, `dirty` o `unknown`;
- un commit final ausente no implica fallo;
- las rutas de archivos siguen siendo relativas aunque Git esté presente;
- el historial AURA no reemplaza el historial Git y Git no reemplaza el registro semántico de sesión.

## 16. Copia, clonado y fork

- **Copiar/mover:** mismo proyecto, conserva `projectId`.
- **Clonar repositorio para continuar el mismo trabajo:** mismo proyecto, conserva `projectId`.
- **Fork deliberado para crear un proyecto nuevo:** nuevo `projectId`; un futuro comando/procedimiento debe copiar lo necesario, registrar `derivedFromProjectId` en el nuevo manifest o historial y evitar que ambas identidades se confundan.

La detección automática de intención de fork queda fuera de v1.0.

## 17. Privacidad y seguridad

Está prohibido registrar en manifest, estado, historial, ADR, backlog o campos libres:

- contraseñas, API keys, tokens, credenciales y secretos;
- claves privadas, certificados privados y connection strings con secretos;
- variables de entorno sensibles;
- transcripts que contengan esos valores.

Antes de persistir, el agente debe redactar secretos y reemplazarlos por una referencia no sensible, por ejemplo `[SECRET_REDACTED]`. No se deben almacenar valores parcialmente identificables si facilitan reconstruir el secreto.

`localProjectPath`, `machineName`, `osUser` y `logicalUser` son metadatos potencialmente personales. Son opcionales y configurables por política del proyecto; la recomendación por defecto es omitir la ruta local y minimizar nombres personales. `machineId` es más sensible que `machineName` y debe registrarse solo cuando aporte trazabilidad, nunca como identidad del proyecto. Los archivos de memoria deben respetar las reglas de versionado, acceso y exclusión de datos sensibles del proyecto.

Las referencias externas deben usar identificadores seguros; no incluir URLs con credenciales ni datos personales innecesarios.

## 18. Versionado y compatibilidad

Toda manifestación conforme declara:

- `protocol`: `AURA_PROJECT_MEMORY`;
- `protocolVersion`: `1.0`;
- `schemaVersion`: `1.0` para el manifest.

Reglas:

- versiones menores agregan campos/documentos opcionales sin cambiar el significado obligatorio;
- versiones mayores pueden cambiar contratos y requieren migración explícita;
- un consumidor debe ignorar campos desconocidos que no necesite;
- un consumidor no debe tratar un `protocolVersion` mayor como compatible sin verificarlo;
- futuras migraciones deben conservar `projectId`, historial y trazabilidad, salvo un fork deliberado;
- la migración no se implementa en esta etapa.

`protocolVersion` identifica la semántica de Project Memory; no debe confundirse con la versión de `AURA_PROTOCOL`, de un rol o del software que lo implementa.

## 19. Errores e inconsistencias

Son inconsistencias de alta prioridad: ausencia de `projectId`, IDs distintos entre Core, duplicación de `sessionId`, rutas absolutas usadas como identidad, historial sobrescrito, secretos registrados o divergencia entre estado y evidencia no declarada.

Respuesta normativa:

1. detener la operación que pueda agravar la inconsistencia;
2. preservar los archivos y registros existentes;
3. marcar el estado como inconsistente o pendiente;
4. reunir evidencia disponible;
5. reconciliar explícitamente y registrar la resolución;
6. no atribuir sincronización o éxito que no pueda verificarse.

Un archivo faltante puede indicar instalación incompleta. No se crea automáticamente una nueva identidad para “arreglar” un proyecto que ya tiene historial.

## 20. Integración con protocolos AURA

AURA Project Memory es una capa documental transversal:

- `AURA_PROTOCOL` continúa definiendo protocolo operativo, descubrimiento de versión, autoridad, control, eventos, autenticación, leases, estado externo y read-back cuando corresponda.
- `AURA_PROTOCOL_CODING_AGENT` continúa definiendo conducta y responsabilidades específicas del agente codificador. Esta especificación no lo reemplaza ni inventa una nueva versión.
- Futuros roles AURA pueden usar el mismo Core y lifecycle; solo cambia `auraRole` y su protocolo de autoridad.
- Al abrir una sesión, el agente debe cargar el contexto local de Project Memory y, si la tarea es operativa AURA, consultar además el protocolo y la fuente de estado que ese protocolo determine.
- Al cerrar una sesión relevante, el registro documental no sustituye un evento AURA obligatorio ni permite afirmar que un estado externo fue actualizado si el protocolo operativo no lo confirmó.
- `projectId` es identidad documental local; no se debe asumir que equivale a `projectCode`, `tenantId` u otra clave de un sistema AURA externo sin una integración futura explícita.

Puntos de integración futuros: bootstrap de agentes, mapping entre `projectId` y proyectos AURA externos, publicación/lectura de sesiones, validación de manifest, manejo de conflictos y procedimientos de fork/migración. Ninguno se implementa en v1.0.

## 21. Criterios de conformidad

Un proyecto cumple AURA Project Memory v1.0 si puede demostrar que:

1. tiene `projectId` estable en `.aura/project.json`;
2. conserva ese ID al copiar, mover o clonar para el mismo proyecto;
3. no usa ruta, máquina, usuario o agente como identidad;
4. distingue `logicalUser`, `osUser`, `machineId`, `machineName`, `agent`, `model`, `auraRole` y `sessionId`;
5. reconoce `PROJECT_ROOT` mediante la manifestación local y soporta ejecución desde subdirectorios;
6. contiene el Core obligatorio y no exige módulos no usados;
7. separa proyecto, estado actual, decisiones, arquitectura, backlog e historial;
8. registra sesiones relevantes con `sessionId` único y campos semánticos mínimos;
9. mantiene `docs/historial/` append-only y evita sobrescrituras;
10. define reconciliación ante contradicciones y concurrencia;
11. funciona sin Git y aprovecha Git solo cuando está disponible;
12. usa timestamps ISO 8601 sin ambigüedad y nombres resistentes a colisiones;
13. prohíbe secretos y permite minimizar metadatos personales;
14. declara `protocolVersion` y `schemaVersion` y prevé evolución;
15. no contiene una copia ni una reinterpretación de los protocolos AURA de rol/operación;
16. permite reconstruir razonablemente el estado vigente aunque desaparezcan los chats.

## 22. Decisiones abiertas de v1.0

Quedan deliberadamente abiertas para etapas posteriores:

- algoritmo concreto de detección de `PROJECT_ROOT` y resolución de monorepos ambiguos;
- herramienta/formato de locking o reconciliación multi-PC;
- procedimiento oficial de fork y migración;
- catálogo de estados de proyecto y de roles futuros;
- política concreta de retención, cifrado y publicación del historial;
- integración formal entre `projectId` y `projectCode`/`tenantId` externos;
- distribución de bootstraps y mecanismo de descubrimiento de protocolos para consumidores;
- si `AURA.md` y `AGENTS.md` deben generarse automáticamente o solo documentarse.

Estas aperturas no impiden la conformidad documental de v1.0 y no autorizan implementar herramientas en esta etapa.
