# ROADMAP — ABM administrativo de proyectos y credenciales

**Última actualización:** 2026-09-28  
**Estado global:** implementación local en curso de verificación; migración y smoke operativo pendientes. Producción no tocada.

| Etapa | Resultado | Estado |
|---|---|---|
| R0 — Auditoría | Revisar v2.3, integración, credenciales, proyectos, usuarios, login, roles y auditoría. | Completada |
| R1 — Seguridad y modelo | Autorización portal `PORTAL_LOGIN` + allowlist admin por tenant; compatibilidad backend Admin API key; scope `AgentCredentialProject`; transición legacy explícita; auditoría append-only. | Implementada en código local |
| R2 — ABM de proyectos | Listar/crear/actualizar/activar/pausar/desactivar, unicidad tenant/código, no borrado físico, auditoría de actor/fechas. | Implementada en código local |
| R3 — ABM de credenciales | Crear `INGESTION` con uno o más proyectos; rotar/revocar; listados sin secreto; vista one-time/copy y variables para Devin. | Implementada en código local |
| R4 — Protección del canal | Revalidar credencial, expiración, estado de proyecto y asignación para cada POST/read-back; scope revocado bloquea JWT existente. | Implementada en código local |
| R5 — Verificación automatizada | Unit tests, integración HTTP/SQL, portal typecheck/lint/build. | Unit tests/build frontend; integración SQL pendiente de entorno |
| R6 — Migración revisada | Revisar cambios aditivos del schema migrator; ejecutar preflight y revisar backup verificado. | Código revisado; backup/migración NO ejecutados |
| R7 — Deploy controlado | Aplicar schema-only vía script explícito, desplegar API/portal, configurar allowlist humana y verificar endpoints. | Pendiente de autorización/entorno |
| R8 — Smoke/read-back | Crear o reutilizar `SMT`, crear credencial del tenant 1 asignada a SMT, probar token, escribir evento de prueba, leerlo, comprobar rechazo en proyecto no asignado, rotar/revocar. | Pendiente; no se enviaron eventos reales |
| R9 — Activación | Confirmar monitoreo, audit, gestión segura de secretos y actualizar estado operativo v2.3. | Pendiente |

## Gates antes de R7/R8

1. Confirmar que el esquema legacy real tiene las claves esperadas y que no hay conflictos de códigos por tenant.
2. Crear backup de AURA y verificarlo con el procedimiento del DBA (`RESTORE VERIFYONLY` o mecanismo institucional equivalente).
3. Ejecutar `scripts/project-control-schema-update.ps1 -SchemaOnly -TargetServer <servidor> -BackupVerified`; el switch es una atestación explícita, no sustituye la verificación del backup.
4. No usar modo que inserta semillas para esta migración. El script `SchemaOnly` no crea ni modifica proyectos/usuarios existentes.
5. Configurar `Admin:TenantAdminUserIds:<tenant>` solo con usuarios humanos autorizados. Sin configuración, acceso denegado.
6. Desplegar únicamente a entorno aprobado. No producción hasta autorización expresa.
7. Ejecutar smoke con proyecto/credencial de prueba y read-back; no usar la contraseña humana como credencial técnica.

## Criterio de PASS

La etapa operativa solo cierra PASS con ABM disponible, credencial nueva `INGESTION` restringida, secreto visto una vez, token válido, evento de SMT aceptado/read-back verificado, intento contra proyecto no asignado rechazado, rotación/revocación validadas, integración SQL en verde, backup verificado y documentación actualizada. La entrega local no equivale a PASS operativo.
