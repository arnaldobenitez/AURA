# AURA - memoria compartida por proyecto

Al iniciar, reanudar o finalizar una tarea significativa:

1. Localiza la raíz del proyecto activo.
2. Lee obligatoriamente `<PROJECT_ROOT>/rules/AURA.md`.
3. Considera ese archivo como la única fuente vigente del protocolo AURA para el proyecto.
4. No utilices copias globales o privadas anteriores del protocolo.
5. Si `rules/AURA.md` no existe durante la instalación inicial, créalo con la plantilla canónica de la versión `2026-08-16.2`.
6. Ejecuta `aura agent bootstrap` o su equivalente programático para obtener el contexto estructurado de Project Memory: `projectId`, documentos Core, módulos, sesiones abiertas, identidad de máquina y contexto Git.
7. Si una tarea es relevante (cambio de código, decisión, bloqueo, análisis sustancial, etc.), abre una sesión con `aura project session start` antes del primer cambio persistente.
8. Actualiza `docs/01-ESTADO-ACTUAL.md` cuando el estado del proyecto cambie, y otros módulos opcionales (`02-DECISIONES`, `03-BACKLOG`, `04-ARQUITECTURA`) solo si existen y la tarea lo requiere.
9. Cierra la sesión con `aura project session close` y declara `updatedDocuments[]` cuando actualices documentación.
10. Obtén el estado operativo actual mediante la API de AURA cuando el protocolo operativo lo requiera; no lo deduzcas de la memoria.
11. Determina dinámicamente el agente, rol y modelo real de la ejecución.
12. El IDE es solamente el entorno de trabajo y nunca constituye la identidad del actor.
13. No guardes en la memoria compartida identidades, modelos, etapas, porcentajes, tokens, credenciales, remote URLs ni datos sensibles.
