# AURA Project Memory

Este proyecto utiliza `AURA_PROJECT_MEMORY v1.0` y `AURA_AGENT_CONTRACT v1.0`.

## Al comenzar una tarea relevante

1. Ejecutar `aura agent bootstrap` para obtener contexto estructurado (`PROJECT_ROOT`, `projectId`, documentos a leer, sesiones abiertas, etc.).
2. Leer `.aura/project.json` y confirmar `projectId`.
3. Leer `docs/00-PROYECTO.md`.
4. Leer `docs/01-ESTADO-ACTUAL.md`.
5. Cargar módulos adicionales solo si la tarea los necesita.
6. Abrir una sesión (`aura project session start`) antes del primer cambio persistente si la tarea es relevante.
7. Consultar además los protocolos operativos AURA aplicables al rol, sin copiarlos en este archivo.

## ProjectControl y Roadmap

- Antes de operar sobre ProjectControl, consultar `GET /api/aura-protocol` y leer el documento indicado por `sourceDocument`. No fijar una versión en las instrucciones del agente ni usar una versión supersedida como autoridad.
- Antes de trabajar una etapa, consultar el tablero y el Roadmap vigente por proyecto/EXEC/workStream. Usar `stageCode`, criterios de aceptación y evidencia verificable al registrar el progreso.
- Solo una credencial `AI_ANALYST` asignada a la etapa puede emitir `APPROVAL` con `stageOutcome = PASS` o `PASS_WITH_WARNINGS`; el CODER no puede aprobar su propio trabajo. `PASS_WITH_WARNINGS` cuenta como hito cerrado, pero sus advertencias deben permanecer registradas.
- Después de toda escritura, hacer read-back del Roadmap/tablero antes de informar éxito. No usar SQL como alternativa a la API.

## Al cerrar

Si la sesión fue relevante, actualizar el estado consolidado y registrar una entrada semántica nueva en `docs/historial/` usando `aura project session close`. No sobrescribir entradas históricas ni guardar transcripts o secretos.

La especificación normativa completa, si está disponible en este repositorio, es `docs/aura/AURA_PROJECT_MEMORY_v1.0.md`. El contrato de integración de agentes es `docs/aura/AURA_AGENT_CONTRACT_v1.0.md`. Este archivo es solo un bootstrap; no es una copia del estándar.
