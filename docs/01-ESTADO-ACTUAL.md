# Estado actual

Última actualización: 2026-09-06
Última sesión: `94693894-df73-415c-bf17-08a5e94b52a6`
Estado general: Project Memory operativo; Project Manager MVP endurecido y adopción documentada.

## Resumen ejecutivo

Etapa 9 completada a nivel local: el sistema cuenta con launcher reproducible, guía de adopción,
smoke test sin red, detección segura de inicialización parcial y un puntero `rules/AURA.md` inequívoco.
Project Manager y el ciclo Project Memory continúan funcionando sin cambios de protocolo estable.

## Implementado

- `tools/aura.ps1` ejecuta el CLI desde cualquier ubicación del repositorio sin modificar PATH ni instalar herramientas globales.
- `tools/smoke-project-memory.ps1` cubre init, validate, bootstrap, sesiones, Project Manager, historial y handoff usando temporales.
- `project init` detecta Core parcial sin `.aura/project.json` mediante `APM014`, no sobrescribe y recomienda revisión manual.
- Guía práctica en `docs/aura/AURA_PROJECT_MEMORY_ADOPTION.md`.
- `rules/AURA.md` declara explícitamente sus fuentes normativas actuales y la no prevalencia del contenido legacy.
- Tests de inicialización parcial y de protección documental.

## En curso

Nada dentro del alcance de Etapa 9.

## Pendiente inmediato

No hay pendientes para el cierre de Project Memory. La instalación global de `aura` y una eventual UI web quedan
fuera de alcance y pueden evaluarse en etapas posteriores.

## Bloqueos

Ninguno funcional. El ejecutable global `aura` no está instalado; se dispone de `tools/aura.ps1` y del comando
`dotnet run --project src/AURA.ProjectMemory.Cli/AURA.ProjectMemory.Cli.csproj -- ...`.
El SDK `10.0.201` fijado por `global.json` no está instalado localmente; se validó temporalmente con `10.0.400`
y `global.json` quedó restaurado sin cambios persistentes.

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

- Build completo `AURA.sln`: correcto, 0 errores y 0 warnings.
- `AURA.ProjectMemory.Tests`: 154 superados.
- `AURA.UnitTests`: 9 superados.
- `AURA.IntegrationTests`: 7 superados y 66 skips preexistentes.
- Smoke test de adopción: correcto, con índice y carpeta temporales.
- `project validate`, `agent bootstrap`, `manager inspect`, `manager handoff` y launcher: correctos.
- `git diff --check`: correcto.

## Próximo paso recomendado

Ejecutar `agent bootstrap` y leer este documento antes de cualquier nueva tarea relevante. Para entregar continuidad,
usar `tools/aura.ps1 manager handoff <path> --json` y abrir una nueva sesión antes del primer cambio persistente.
