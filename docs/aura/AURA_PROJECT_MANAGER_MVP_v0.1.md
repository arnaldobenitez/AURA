# AURA Project Manager MVP v0.1

**Estado:** experimental; no es un protocolo AURA estable.

## Propósito

Project Manager es una superficie reutilizable para inspeccionar proyectos AURA Project Memory y preparar
continuidad para otro agente. Reutiliza `ProjectRootResolver`, `ProjectMemoryValidator`,
`ProjectContextService`, `GitContextProvider` y `SessionStateStore`; no duplica las reglas normativas ni
crea sesiones al leer un proyecto.

## Comandos

```text
aura manager inspect <path> [--take <n>] [--json]
aura manager remember <path> [--alias <name>] [--json]
aura manager list-known [--json]
aura manager forget <alias-or-projectId> [--json]
aura manager history <path> [--take <n>] [--json]
aura manager handoff <path> [--take <n>] [--json]
```

`--index-path <file>` está disponible como opción técnica para pruebas o perfiles aislados. `--machine-path`
permite seleccionar explícitamente el perfil local de máquina cuando aplica.

- `inspect` devuelve identidad, versión de Project Memory, validez, diagnósticos, Core, módulos opcionales,
  máquina, Git, sesiones abiertas e historial reciente.
- `remember` valida y registra el proyecto en el índice local. Es idempotente por `projectId`.
- `list-known` muestra los proyectos recordados y si cada path existe y sigue siendo válido.
- `forget` elimina solamente una entrada del índice por alias o `projectId`.
- `history` lee los últimos registros Markdown del historial declarado, ordenados por timestamp descendente.
- `handoff` genera una síntesis de continuidad con estado actual, Git, sesiones, historial, advertencias,
  próximos pasos y el comando recomendado de bootstrap.

Las operaciones de lectura (`inspect`, `list-known`, `history` y `handoff`) no crean sesiones ni modifican el
proyecto. `remember` y `forget` solo modifican el índice local del usuario.

## Índice local

La ubicación predeterminada es:

```text
~/.aura/projects/index.json
```

`remember` crea el directorio y el archivo si no existen. Cada entrada contiene únicamente:

- `projectId`;
- `alias` opcional;
- `projectName`;
- `projectRoot`;
- `rememberedAt`;
- `lastSeenAt`.

No se guardan secretos, tokens, contenido de documentos, remotos Git ni perfiles de máquina. Un alias no puede
reutilizarse para otro `projectId`.

## Relación con Project Memory y Agent Bootstrap

Project Manager resuelve el mismo `PROJECT_ROOT` que Project Memory y consume sus documentos Core. `agent bootstrap`
sigue siendo el punto de entrada normativo para un agente: Project Manager es una vista operativa adicional y no
reemplaza el contrato de agente, la validación, las sesiones ni el historial append-only.

El handoff no es una transcripción y no sustituye `docs/01-ESTADO-ACTUAL.md`. Resume lo necesario para que un agente
pueda volver a ejecutar bootstrap y continuar con contexto.

## Límites del MVP

- No existe UI web, base de datos, red, sincronización ni locking distribuido.
- El índice es local a la máquina y no forma parte de Project Memory portable.
- El historial se interpreta de forma conservadora: si un nombre o sección no puede parsearse, se omite el dato.
- No se modifica `AURA_PROJECT_MEMORY v1.0`, `AURA_AGENT_CONTRACT v1.0`, `projectId` ni `machineId`.
- La instalación del ejecutable global `aura` queda fuera de esta etapa; puede utilizarse el CLI del repositorio con
  `dotnet run --project src/AURA.ProjectMemory.Cli/AURA.ProjectMemory.Cli.csproj -- ...`.

## Próximos pasos

Evaluar una futura integración con una superficie web o un índice multi-proyecto más completo sin convertir este
MVP experimental en protocolo estable antes de validar su interoperabilidad y privacidad.
