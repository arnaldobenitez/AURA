> **Resolución:** el Analista Funcional eligió la opción recomendada en ambos gates (Gate 1:
> `sWorkStream` nullable aditivo; Gate 2: tabla `AgentCredential` + HMAC vía variable de entorno)
> y "implementar todo lo posible localmente" como alcance. Ver
> [AURA-PROTOCOL-V2-01-Informe.md](AURA-PROTOCOL-V2-01-Informe.md) para el resultado.

# AURA-PROTOCOL-V2-01 — Gate de diseño (auditoría + decisiones antes de implementar)

Este documento cumple la auditoría obligatoria y los dos gates arquitectónicos que el prompt
de `AURA-PROTOCOL-V2-01` exige presentar **antes** de escribir código o aplicar migraciones.
No se implementó nada de autenticación/concurrencia todavía.

## Auditoría (verificada contra el código real, no contra memoria/documentación previa)

- **Autenticación:** ninguna, en ningún endpoint, en ningún entorno. Confirmado en `Program.cs`
  (sin middleware de auth), sin paquetes `Microsoft.AspNetCore.Authentication.*` ni `Identity`
  referenciados en ningún `.csproj`. `appsettings.json` no tiene ninguna clave de seguridad.
- **Tenant:** no existe una tabla/entidad `Tenant`. `idTenant`/`tenantId` es un `int` suelto
  repetido en cada tabla (`ProProyectosCabecera`, `ProProyectosItems`, `Usuarios`,
  `ProCodificadores`, `ProProyectosAnalistas`), sin catálogo propio ni validación de que el
  tenant "exista".
- **Concurrencia actual:** no hay columna de versión/concurrency-token (`rowversion`,
  `Timestamp`, etc.) en ninguna tabla. El único mecanismo existente es
  `expectedPreviousItemId` sobre `ProProyectosItems`, calculado dentro de una transacción con
  `UPDLOCK, HOLDLOCK` sobre las filas del proyecto — es decir, la unidad de exclusión hoy es
  **todo el proyecto**, no una etapa/workstream/tarea.
- **Control actual:** `nControlTipo`/`nControlId` en el último item del proyecto — un único
  `CONTROL` por `(tenantId, projectCode)`. No existe lease, expiración ni heartbeat: un `CLAIM`
  nunca "vence" por sí mismo.
- **`actor.type = HUMAN` para `REQUEST`/`APPROVAL`:** confirmado, enforced en
  `ProjectControlService.Validate` (no es documentación desactualizada; es una regla real y
  activa hoy).
- **Workstream/Task:** no existen en ningún nivel (ni columna, ni tabla, ni DTO) — confirmado de
  nuevo en esta auditoría, igual que en `AURA-PROTOCOL-V2-00`.
- **Catálogos de identidad existentes** (`Usuarios`, `ProCodificadores`, `ProProyectosAnalistas`):
  son puramente catálogos de *nombre para mostrar*, sin columna de secreto/hash ni de ningún
  tipo de credencial. Se usan hoy solo para resolver `sNombre` en las respuestas del tablero.
- **Deployment de `aura.aguarapp.com`:** `scripts/publish.ps1` **solo** genera localmente
  `Versiones/<fecha> v.X.X.XX/{Api,Web}`; no toca el servidor OMEN. Llevar esa carpeta a
  `d:\Produccion\aura` en OMEN y reintroducir la connection string en el `appsettings.json`
  desplegado es, hoy, un paso **manual** del usuario sobre una máquina a la que este agente no
  tiene acceso. Consecuencia directa: **la activación productiva real no puede completarse de
  forma autónoma por este agente** en ningún escenario — como máximo puedo dejar todo listo
  (código, tests, script de publish, guía de verificación) para que el despliegue manual sea el
  único paso pendiente humano.

## GATE 1 — Granularidad de concurrencia (Workstream)

**Problema:** el ejemplo obligatorio del prompt (`Project A/Workstream X/Task 1 → Agent A` y
`Project A/Workstream Y/Task 2 → Agent B` trabajando simultáneamente sin bloquearse) requiere
una unidad de exclusión más fina que "proyecto completo". Hoy esa unidad es exactamente el
proyecto completo — dos agentes en el mismo proyecto siempre compiten por el mismo `CONTROL` y
la misma cadena `expectedPreviousItemId`, sin importar si sus tareas son realmente
independientes. Sin `Workstream` (o equivalente), este requisito no es alcanzable.

**Alternativas:**

| Opción | Descripción | Costo/riesgo |
|---|---|---|
| **A. No hacer nada ahora** | Mantener exclusión a nivel Proyecto. | Cero riesgo/cambio, pero dos agentes legítimamente independientes en el mismo proyecto (p. ej. SiniTrax/AFD vs SiniTrax/WhatsApp) seguirían bloqueándose entre sí — el requisito de concurrencia del prompt queda explícitamente sin cumplir. |
| **B. `sWorkStream` nullable (recomendada)** | Columna de texto libre nullable en `ProProyectoEXEC` y `ProProyectosItems` (mismo patrón aditivo que ya usa todo el esquema: `sCodigo`, `sRepositorio`, etc.). El lease/control pasa a ser único por `(tenantId, projectCode, workStream)` en vez de `(tenantId, projectCode)`. `workStream = NULL` preserva exactamente el comportamiento actual para proyectos sin subdivisión (AURA, TurnoMedix, Contadoro, etc.). | Cambio de contrato (nuevo campo opcional en `RegisterProjectEventRequest`/board), migración aditiva de esquema, ajuste del cálculo de exclusión — acotado pero real. |
| **C. Entidad `ProProyectoWorkStream` normalizada** | Tabla catálogo propia con PK, en vez de texto libre. Más correcto a largo plazo (nombres estables, posibilidad de metadata propia por workstream). | Mayor superficie de migración y de cambio de contrato para esta etapa; no hay hoy un requisito concreto que justifique más que un nombre estable. |

**Migración que implicaría (si se aprueba B):** aditiva, sin romper nada existente —
`ALTER TABLE ... ADD sWorkStream nvarchar(100) NULL` sobre `ProProyectoEXEC` y
`ProProyectosItems` (mismo patrón que `ProjectControlSchemaMigrator` ya usa), más un índice de
soporte para la nueva clave de exclusión. **Rollback:** eliminar la columna (no hay pérdida de
datos existentes, ya que es nullable y no se popula retroactivamente).

**Compatibilidad:** total con proyectos que nunca envíen `workStream` (comportamiento idéntico
al actual). Ningún consumidor existente se rompe.

**Recomendación:** **B**. Es el mínimo cambio que hace alcanzable el requisito obligatorio de
concurrencia del prompt, preserva compatibilidad total, y sigue el mismo patrón aditivo que ya
usa el resto del esquema — sin comprometerse todavía a una tabla catálogo normalizada que nada
en el sistema real necesita hoy.

## GATE 2 — Almacenamiento de credenciales y firma de JWT

**Problema:** no existe ningún lugar seguro para credenciales de agente ni ninguna clave de
firma. Los catálogos existentes (`Usuarios`/`ProCodificadores`/`ProProyectosAnalistas`) son
catálogos de nombre, no de identidad autenticable, y mezclar ambas cosas en la misma tabla
arriesga tratar material de seguridad con el mismo descuido con que hoy se tratan nombres
(el propio código ya documenta inconsistencias de catálogo, p. ej. el codificador legado
llamado literalmente "Vs Code").

**Alternativas:**

| Opción | Descripción | Riesgo |
|---|---|---|
| **A. Reusar `ProCodificadores`** agregándole una columna de hash de secreto. | Cero tablas nuevas. | Mezcla identidad de visualización con material de autenticación; alto riesgo de que una edición despreocupada del catálogo (ya documentada como propensa a inconsistencias) afecte seguridad. **No recomendada.** |
| **B. Tabla nueva `AgentCredential` (recomendada)** | Aditiva, mismo patrón que `ProProyectoEXEC`/`ProProyectoEtapa`: `IdTenant, IdCredential (PK), AgentRole, AgentName, SecretHash (salted), CreatedAtUtc, RevokedAtUtc, LastUsedAtUtc, RotatedFromCredentialId`. Nunca guarda el secreto en texto plano — solo su hash. | Migración nueva, acotada; sin impacto en tablas existentes. |
| **C. Credenciales solo en configuración** (User Secrets/env var, sin tabla). | Cero migración. | No permite revocar/rotar sin redeploy, no registra `LastUsedAtUtc`, no cumple el detalle explícito del prompt sobre administración de credenciales. Podría servir como paso 0 transitorio, no como solución. |

**Clave de firma del JWT:** simétrica (HMAC-SHA256) desde variable de entorno/User Secret —
igual convención que ya usa `ConnectionStrings:DefaultConnection` (nunca en
`appsettings*.json`, nunca versionada). Es proporcional porque la propia API es la única que
emite y valida los tokens (no hay un servicio de recursos separado que justifique un par de
claves asimétrico RSA todavía); si en el futuro aparece un segundo validador independiente,
migrar a RSA es un cambio aislado a la emisión/validación, no al resto del protocolo.

**Administración de credenciales:** sin UI completa por ahora (el prompt lo permite
explícitamente). Se propone una operación administrativa protegida (script + endpoint con un
secreto administrativo separado, mismo patrón que `project-control-schema-update.ps1`) para
crear/revocar/rotar/listar metadata — nunca exponiendo el secreto original una vez emitido.

**Migración que implicaría (si se aprueba B):** `CREATE TABLE AgentCredential` nueva (aditiva,
no toca tablas existentes). **Rollback:** `DROP TABLE` (sin impacto en datos de Control de
Proyectos IA, que no depende de esta tabla para nada de lo ya implementado).

**Recomendación:** **B** + firma HMAC vía variable de entorno.

## Alcance de esta pasada dado el bloqueo de despliegue manual

Independientemente de las decisiones anteriores, **la activación en `https://aura.aguarapp.com`
no puede completarse de forma autónoma** (ver auditoría). Lo máximo que puedo entregar en esta
tarea es: código + migraciones locales aplicadas y verificadas contra la base de desarrollo +
tests + smoke multiagente local + guía de verificación productiva — dejando el `Copy-Item` a
OMEN y la reintroducción de la connection string como el único paso manual pendiente,
exactamente igual que ya ocurre hoy con cualquier otra publicación de AURA.
