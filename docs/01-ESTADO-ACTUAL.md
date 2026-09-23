# Estado actual

Última actualización: 2026-09-23
Última sesión: `adcfa998-d4af-4a46-b1e2-a0e5158febba`
Estado general: Project Memory operativo; AURA ProjectControl incorpora Roadmap localmente. Migración y despliegue pendientes.

## Resumen ejecutivo

Post Etapa 9, los cambios de Project Memory quedaron ordenados en commits lógicos y se completó el push
manual previo. Roadmap y contrato v2.2 quedaron registrados en `19b87fb`; el adaptador de memoria se actualizó
en `0193d1f`; la metadata de la publicación 0.8.00 quedó en `4543e28`. La migración SQL aditiva no se aplicó
y producción no se modificó. Esta sesión agrega el historial del saneamiento de commits.

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

## En curso

Roadmap v0.1 implementado en código y contrato localmente; sin aplicar el cambio de esquema SQL ni desplegar.

## Pendiente inmediato

1. Aplicar `scripts/project-control-schema-update.ps1` únicamente en la base de desarrollo autorizada; la migración no se ha ejecutado.
2. Ejecutar las pruebas ProjectControl contra SQL Server después de la migración.
3. Realizar despliegue y smoke test de producción cuando Arnaldo lo autorice; no se ejecutó en esta sesión.

## Bloqueos

No hubo cadena de conexión de SQL Server de test disponible en el entorno: 68 pruebas de integración quedaron omitidas
por su gate existente. La migración es explícita y debe aplicarse antes de usar las nuevas rutas con una base.

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
- `AURA.UnitTests`: 11 superados, incluyendo validación de roles/planes Roadmap.
- `AURA.ProjectMemory.Tests`: 154 superados.
- `AURA.IntegrationTests`: 7 superados, 68 omitidos por no disponer de SQL Server de test; las pruebas nuevas Roadmap requieren esa conexión.
- `AURA.Web`: `npm run build` y `npm run lint` correctos; oxlint 0 warnings/errores.
- `git diff --check`: correcto (solo advertencias de conversión LF/CRLF de Git).

## Próximo paso recomendado

Antes de probar Roadmap con SQL, aplicar el migrador explícitamente en la base de desarrollo autorizada y ejecutar las pruebas de integración nuevas. No desplegar a producción sin autorización y smoke test controlado. Para otra tarea relevante, ejecutar bootstrap y abrir una sesión antes del primer cambio persistente.
