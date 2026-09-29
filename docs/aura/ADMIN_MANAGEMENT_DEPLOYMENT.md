# Despliegue controlado — ABM administrativo

**Estado:** procedimiento preparado; no ejecutado. No implica autorización de producción.

## 1. Configuración administrativa

El ABM del portal usa sesión humana (`POST /api/auth/login`) y un JWT con claim `aura:authSource=PORTAL_LOGIN`. El backend compara `aura:tenantId` + `aura:linkedActorId` contra el allowlist del tenant:

```text
Admin:TenantAdminUserIds:<tenantId> = <IdUsuario autorizado>[,<IdUsuario autorizado>...]
```

Ejemplo de nombre de setting, con ID ficticio: `Admin__TenantAdminUserIds__1=17,23`. El operador debe obtener los IDs reales del administrador por el procedimiento de identidad institucional; no se asigna por correo declarado ni desde un evento. Si no hay allowlist, la autorización falla cerrada. La variable se guarda solo en configuración del servidor.

`Admin:ApiKey` es compatibilidad backend/break-glass; nunca se incorpora a `AURA.Web`, builds Vite, localStorage/sessionStorage ni peticiones desde navegador. El secreto de ingestión del agente es distinto.

## 2. Preflight y backup

1. Confirmar servidor/base correctos con operador DBA y ejecutar las verificaciones de esquema y duplicados que correspondan para `ProProyectosCabecera` (`idTenant`, `IdProyecto`, `sCodigo`).
2. Generar backup de `AURA` con el método autorizado del entorno.
3. Verificar integridad/restaurabilidad con `RESTORE VERIFYONLY` o procedimiento equivalente; registrar ubicación y resultado fuera del repositorio. No incluir connection strings ni datos sensibles en la bitácora.
4. Revisar el diff de `ProjectControlSchemaMigrator.ApplySchemaOnlyAsync`: adiciones a `ProProyectosCabecera`, `AgentCredential`, `AgentCredentialProject`, `AuraAdminAudit` e índices/FK. No ejecutar todavía el modo que agrega seeds.

## 3. Aplicación de esquema

El migrador es explícito y nunca corre al iniciar la API. El script exige declaración manual del backup:

```powershell
$env:AURA_SCHEMA_MIGRATION_CONNSTR = '<connection string inyectada de forma segura>'
./scripts/project-control-schema-update.ps1 -SchemaOnly -TargetServer '<servidor aprobado>' -BackupVerified
```

El valor de conexión se inyecta por el mecanismo seguro del operador, no se pega en terminal compartida, prompt, historial ni logs. `-BackupVerified` es una confirmación del operador; el script no crea/verifica el backup. No pasar `-Force` en `SchemaOnly`.

En esta iteración no se ejecutó el script, no se aplicó DDL y no se contactó producción.

## 4. Postchecks después de una futura aplicación autorizada

- Verificar objetos/índices administrados por el migrador y confirmar transacción completada.
- `GET /api/admin/authorization` desde portal: administrador configurado obtiene 200; login no autorizado obtiene 403; anónimo 401.
- CRUD de proyecto en tenant de test, unicidad `(tenant, projectCode)`, estados `PAUSED`/`ARCHIVED`; comprobar que no hay DELETE físico.
- Crear credencial `INGESTION` de prueba con un proyecto asignado; revisar hash-only en persistencia por medio del control DBA, nunca obtener el secreto mediante SQL/GET.
- Intercambiar en `/api/auth/token`; crear un evento de prueba en proyecto autorizado y verificar read-back; otro proyecto del mismo tenant debe recibir 403.
- Rotar: nuevo secreto se presenta una sola vez; el anterior falla en token exchange. Revocar: token futuro falla y JWT vigente no puede escribir/read-back.
- Verificar que `AuraAdminAudit` contiene metadatos de operaciones, no plaintext de credencial/password/JWT.
- Borrar fixtures solo en base de prueba mediante rutina aprobada. No usar estos postchecks en producción sin autorización expresa.

## 5. Criterio de go-live

No habilitar integraciones externas hasta que pasen todos los postchecks, se configure la allowlist de administradores por tenant, se acuerde la rotación de credenciales legacy tenant-wide, se revise/retire el bootstrap humano con password default preexistente y se rote cualquier cuenta afectada, se confirme si `SMT` ya existe y se complete read-back en el entorno destino. El registro operativo de AURA se mantiene pendiente si no hay credencial de ingestión disponible.
