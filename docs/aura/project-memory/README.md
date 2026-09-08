# AURA Project Memory — estándar físico

Esta carpeta contiene contratos y plantillas reutilizables para proyectos compatibles con `AURA_PROJECT_MEMORY v1.0`.

## Contenido

- `schemas/project.schema.json`: contrato machine-readable de `PROJECT_ROOT/.aura/project.json`.
- `schemas/machine.schema.json`: contrato conceptual del perfil local futuro `~/.aura/machine.json`; no se copia al proyecto.
- `templates/`: plantillas mínimas para Core y módulos opcionales.

Los `$id` de los schemas son identificadores documentales, no requieren red ni disponibilidad de ningún servicio para validar localmente.

## Core obligatorio

Cada proyecto compatible debe contener:

```text
AURA.md
.aura/project.json
docs/00-PROYECTO.md
docs/01-ESTADO-ACTUAL.md
docs/historial/
```

`AGENTS.md` es un adaptador recomendado para herramientas que lo reconozcan, pero no forma parte del Core. `02-DECISIONES.md`, `03-BACKLOG.md`, `04-ARQUITECTURA.md`, ADR y otros módulos son opcionales.

## Contrato operativo de PROJECT_ROOT

Desde el path de trabajo, se inspecciona primero el directorio actual y luego cada ancestro hasta encontrar el primer `.aura/project.json` válido. Ese directorio define `PROJECT_ROOT`; la búsqueda se detiene en el límite del filesystem o workspace explícito. En proyectos anidados prevalece el manifest más cercano. Si el workspace contiene varios proyectos y el contexto no permite desambiguarlos, la herramienta debe solicitar selección explícita. Un `project.json` inválido produce `ERROR` y no autoriza a inventar otro `projectId`.

La ausencia de un documento Core produce `ERROR`; un módulo opcional declarado cuyo archivo no existe produce `WARNING`; metadatos opcionales omitidos, Git ausente o API no disponible producen `INFO` y no invalidan el Core. Una `schemaVersion` mayor que la soportada produce `ERROR` de compatibilidad; una menor soportada puede requerir migración explícita según la futura herramienta. En ningún caso se sobrescribe silenciosamente información existente.

Un fork lógico es distinto de copiar o mover: recibe un nuevo UUID v4, conserva solo la documentación que se decida reutilizar, y debe dejar constancia de `derivedFromProjectId` en el nuevo proyecto o su historial. La copia/movimiento/clonación para continuar el mismo proyecto conserva el ID original.

## Uso futuro

Una futura herramienta podrá copiar estas plantillas, sustituir los marcadores y validar el manifest contra el schema. Esta carpeta no implementa `init`, `validate`, registro de sesiones, sincronización, locking ni integración automática con Git. La automatización de inicialización/validación queda fuera de esta etapa.

Las plantillas son puntos de partida: no deben rellenarse con contenido ficticio ni crear módulos que el proyecto no necesite. Las rutas de documentación dentro del proyecto son relativas a `PROJECT_ROOT`, usan `/` y nunca contienen secretos ni rutas físicas.

## Versiones

`projectMemoryVersion` identifica la semántica de Project Memory. `schemaVersion` identifica la estructura del manifest. La versión de templates se mantiene en este README y en los cambios del estándar; no se agrega un campo de runtime al manifest en v1.0 para evitar una tercera identidad de versión.

El campo `projectMemoryVersion` se usa deliberadamente en lugar de `protocolVersion`: evita confundir esta especificación documental con `AURA_PROTOCOL` y sus versiones operativas. El manifest no registra la versión de ningún protocolo AURA externo.

## Política de valores no aplicables

En templates Markdown se omiten campos que no apliquen. No se obliga a escribir `N/A`, `unknown` o `not-applicable`. Si distinguir “desconocido” de “no aplica” es material para una decisión, se escribe explícitamente en la sección semántica correspondiente.

## Compliance futuro

Un validador posterior debería verificar, como mínimo:

- existencia de los cinco elementos del Core;
- JSON válido y conformidad de `project.json` con el schema;
- UUID v4 válido en `projectId`;
- rutas relativas, sin `..`, separadores físicos ni rutas absolutas;
- existencia de los documentos Core declarados;
- coherencia entre `projectMemoryVersion` y `schemaVersion` soportadas;
- módulos declarados y archivos opcionales presentes;
- ausencia de patrones evidentes de secretos;
- historial append-only y nombres de sesión conforme a la convención;
- ausencia de dependencia obligatoria de Git, API, máquina o agente.

No existe todavía ese validador.
