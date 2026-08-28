# AURA-PROTOCOL-V2-01 — Informe final

Companion: [AURA-PROTOCOL-V2-01-Gate-Diseno.md](AURA-PROTOCOL-V2-01-Gate-Diseno.md) (auditoría
+ los dos gates arquitectónicos, resueltos con el Analista antes de escribir código: **Gate 1 —
Workstream:** `sWorkStream` nullable aditivo, aprobado. **Gate 2 — Credenciales:** tabla nueva
`AgentCredential` + HMAC vía variable de entorno, aprobado. **Alcance:** implementar todo lo
posible localmente, dejando el despliegue productivo manual como único paso pendiente).

## A. Git inicial

Rama `master`, HEAD `97e0790` más los cambios (no comiteados) de `AURA-PROTOCOL-V2-00`. Ningún
cambio ajeno fue absorbido ni descartado durante esta tarea.

## B. Auditoría de seguridad

Confirmado directamente en el código (no en documentación previa): sin autenticación en ningún
endpoint, en ningún entorno; sin paquetes `Authentication.*`/`Identity` referenciados; sin
ninguna clave de seguridad en `appsettings.json`. Ver `AURA-PROTOCOL-V2-01-Gate-Diseno.md` para
el detalle completo de la auditoría (Tenant, concurrencia, control, catálogos, deployment).

## C. Modelo actual

`ProProyectosItems` es un log de eventos append-only; `nControlTipo`/`nControlId` +
`IdControlItem` ya representaban "quién controla" pero sin lease/expiración/versión. Sin
columna de concurrencia (`rowversion`) en ninguna tabla; el único mecanismo previo era
`expectedPreviousItemId` con `UPDLOCK, HOLDLOCK` **a nivel de todo el proyecto**, no por unidad
de trabajo.

## D. Modelo de identidad

Nuevo: `AgentCredential` (`idTenant, IdCredential, nActorTipo, sAgentName, LinkedActorId,
SecretSalt, SecretHash, SecretIterations, CreatedAtUtc, RevokedAtUtc, LastUsedAtUtc,
RotatedFromCredentialId`). `nActorTipo` reutiliza `ProjectActorType` (`HUMAN`, `AI_ANALYST`,
`CODER` — nunca `UNASSIGNED`). `LinkedActorId` es opcional para los tres roles (ver M y
`docs/aura/AURA_PROTOCOL_v2.1.md` §9 para por qué no se hizo obligatorio para HUMAN).

## E. Estrategia de credenciales

Secreto aleatorio de 32 bytes (`RandomNumberGenerator`), nunca almacenado en texto plano: hash
PBKDF2-HMAC-SHA256 con 210.000 iteraciones (recomendación OWASP 2023) + salt de 16 bytes por
credencial. Comparación en tiempo constante (`CryptographicOperations.FixedTimeEquals`) al
validar. El secreto en texto plano se devuelve **una sola vez**, en la creación o rotación.
Administración vía `/api/admin/agent-credentials` (crear/rotar/revocar/listar), protegida por
`X-Admin-Api-Key` — ver `docs/aura/AURA_PROTOCOL_v2.1.md` §2, §10 para por qué no se construyó
una UI completa.

## F. Token/access model

JWT HMAC-SHA256, emitido por `POST /api/auth/token` a partir de credencial+secreto válidos. Vida
configurable (`Jwt:AccessTokenLifetimeMinutes`, 15 min por defecto). Claims:
`aura:tenantId, aura:credentialId, aura:actorType, aura:agentName, aura:linkedActorId` (opcional).
`Jwt:SigningKey` sigue la misma convención que la connection string (User Secrets en desarrollo,
edición manual en producción); fuera de `Development` la API no arranca sin él configurado
explícitamente. En `Development` (incluidos los tests), un placeholder fijo definido en código —
nunca en un archivo versionado — permite operar sin configuración adicional.

## G. Autorización

`ProjectControlService.CheckAuthorization` (nuevo) rechaza, antes de tocar la base de datos:
(1) `tenantId` del request que no coincide con el del token autenticado; (2) `actor.type` del
cuerpo que no coincide con el `ActorType` autenticado de la credencial (esto es lo que vuelve
real, no solo declarativa, la restricción HUMAN-only de `REQUEST`/`APPROVAL`); (3) `actor.id`
que no coincide con `LinkedActorId` cuando la credencial lo fija. No se implementó una lista de
control de acceso por proyecto (ver Z) — deliberado, para no agregar un tercer esquema no
discutido en el gate de diseño.

## H. Project/workstream/task locking

`workStream` (nullable, ≤100 caracteres) en `ProProyectoEXEC` y `ProProyectosItems`. La
resolución de "cadena actual" (`GetChainStateAsync`) filtra por `(idTenant, IdProyecto,
sWorkStream)`; la numeración física de `IdItems`/`IdEXEC` sigue siendo global por proyecto (para
no duplicar el eje de identidad de esas claves primarias) mientras que EXEC/etapa/control/lease
vigentes se resuelven de forma completamente independiente por workstream. Verificado con un test
de dos agentes (credenciales distintas) trabajando en `Workstream X`/`Workstream Y` del mismo
proyecto simultáneamente sin bloquearse.

## I. Lease

Sin tabla nueva: se agregaron `LeaseId (uniqueidentifier)`, `LeaseVersion (bigint)`,
`LeaseExpiresAtUtc (datetime2)` a `ProProyectosItems`, propagados en cada fila igual que el resto
del "snapshot vigente" (mismo patrón que `nPorcEtapa`/`nPorcEXEC`). `CLAIM` adquiere/renueva;
`PROGRESS/HANDOFF/BLOCKER/DEPLOYMENT` deben presentar `LeaseId`+`ExpectedLeaseVersion` vigentes;
`HANDOFF` lo reasigna atómicamente al nuevo control declarado; `COMPLETION` lo libera. El tenedor
real se identifica por la credencial autenticada (`CredentialId`), no por `actor.type`/`id` de
texto libre — con una fase de transición tras `HANDOFF` donde el tenedor declarado (tipo/id) aún
no se ha autenticado bajo ese lease (ver `docs/aura/AURA_PROTOCOL_v2.1.md` §6).

## J. Optimistic concurrency

`ExpectedLeaseVersion` distinto al vigente → `409 LEASE_CONFLICT`. Lease vencido (`ExpiresAtUtc`
en el pasado) → `409 LEASE_CONFLICT` ("lease vencido"). Ambos casos cubiertos por tests
dedicados, incluyendo un lease forzado al pasado vía SQL directo en el fixture de test (sin
esperar el vencimiento real).

## K. Idempotency

Sin cambios al mecanismo existente (`eventId` único, verificado **antes** de resolver el lease,
de forma que un reintento nunca vuelve a validar/incrementar el lease). Verificado que sigue
funcionando exactamente igual que antes de esta tarea.

## L. Handoff

Atómico dentro de la misma transacción que ya envolvía la escritura del evento: el lease se
reasigna (nuevo `LeaseId`, `Version = 1`) al mismo tiempo que se resuelve el nuevo control
(`nControlTipo`/`nControlId`) y se inserta la fila — no hay ventana donde ambos lados crean tener
el control.

## M. HUMAN vs ANALYST

Auditado y **no eliminado ni ampliado automáticamente**: `REQUEST`/`APPROVAL` siguen exigiendo
`actor.type = HUMAN`, ahora verificado contra la identidad autenticada (antes era una validación
de solo el cuerpo del request, sin ninguna garantía de que fuera cierta). `REWORK` permanece sin
restricción de rol, igual que antes de esta tarea — no se encontró ninguna decisión previa de
restringirlo, así que no se restringió unilateralmente. Ver `docs/aura/AURA_PROTOCOL_v2.1.md` §8
para el detalle completo (operaciones HUMAN-only, impacto UX, riesgo de autoaprobación cerrado).

## N. Cambios DB

Aditivos, sin romper nada existente: `ProProyectoEXEC.sWorkStream`;
`ProProyectosItems.{sWorkStream, LeaseId, LeaseVersion, LeaseExpiresAtUtc, IdCredential}`; tabla
nueva `AgentCredential`. Ninguna columna/tabla existente fue alterada o eliminada.

## O. Migraciones

Aplicadas contra la base de desarrollo real (`scripts/project-control-schema-update.ps1` /
`ProjectControlSchemaMigrator`, mismo mecanismo aditivo-únicamente ya usado por
`AURA-PROTOCOL-V2-00`) y verificadas mediante los 67 tests de integración corriendo contra esa
misma base. **Rollback:** eliminar las columnas nuevas (nullable, sin pérdida de datos
existentes) y `DROP TABLE AgentCredential` (no referenciada por ningún dato de Control de
Proyectos IA ya existente).

## P. Endpoints

Nuevos: `POST /api/auth/token`; `POST /api/admin/agent-credentials`,
`POST /api/admin/agent-credentials/{id}/rotate`, `POST /api/admin/agent-credentials/{id}/revoke`,
`GET /api/admin/agent-credentials`. Modificados: `POST /api/project-control/events` y
`POST /api/project-control/projects` ahora exigen `[Authorize]` (JWT); `GET /api/project-control`
y su historial permanecen anónimos (decisión de alcance explícita, ver
`docs/aura/AURA_PROTOCOL_v2.1.md` §9).

## Q. Tests

9/9 unitarios, 67/67 de integración (57 preexistentes, retro-adaptados con token+lease sin
cambiar ningún comportamiento verificado; 10 nuevos cubriendo exactamente la matriz pedida:
credencial inválida/revocada, token expirado, spoofing de actor.type rechazado, aislamiento de
tenant vía token, segundo CLAIM conflictivo entre credenciales distintas, dos workstreams
independientes sin bloquearse, lease vencido, `ExpectedLeaseVersion` obsoleta, renovación de
lease). `verify.ps1` completo (restore, build, test, `npm ci`, typecheck, lint, build de
`AURA.Web`) pasa de punta a punta.

## R. Smoke multiagente

`AuraProtocolV2SmokeTest` (heredado de V2-00, extendido con autenticación real + lease) recorre
`REQUEST(HUMAN) → CLAIM(CODER, adquiere lease) → PROGRESS(CODER, presenta lease) →
HANDOFF(CODER→HUMAN, reasigna lease) → APPROVAL(HUMAN) → COMPLETION(HUMAN, libera lease)` con
read-back en cada paso. Adicionalmente, `RegisterEvent_TwoIndependentWorkstreams_...` demuestra
tres agentes conceptuales (dos credenciales `CODER` reales + el flujo HUMAN del smoke test)
operando sobre unidades independientes sin interferirse, y
`RegisterEvent_SecondClaimByDifferentCredential_ReturnsLeaseConflict` demuestra que un segundo
agente no puede tomar la unidad de otro. Datos de prueba controlados bajo tenants fuera de rango
(900001/900002); ningún historial real reconstruido.

## S. Deployment

**No realizado — no es técnicamente posible desde este agente.** Confirmado en la auditoría:
`scripts/publish.ps1` solo genera `Versiones/<fecha> v.X.X.XX/{Api,Web}` en la máquina local; no
toca el servidor OMEN. Llevar esa carpeta a `d:\Produccion\aura` y reintroducir la connection
string (y ahora también `Jwt:SigningKey`/`Admin:ApiKey`) en el `appsettings.json` desplegado es un
paso manual del usuario sobre una máquina a la que este agente no tiene acceso — exactamente
igual que la connection string ya lo era antes de esta tarea.

## T. Smoke aura.aguarapp.com

**No realizado**, por la misma razón que S. No se usó `localhost` como evidencia de activación en
ningún momento de este informe.

## U. Read-back

Verificado en cada test (board re-leído después de cada escritura relevante) y explícitamente en
el smoke test multi-paso. El contrato de respuesta (`ProjectControlEventResponse.lease`) expone
`leaseId`/`version`/`expiresAtUtc`/`holderActorType`/`holderActorId` en cada escritura aceptada
(salvo `REQUEST`/`COMPLETION`, que no sostienen uno), permitiendo a un cliente confirmar el estado
sin una segunda llamada adicional cuando solo necesita esa información.

## V. Git/commit

Cambios realizados pero **no comiteados** — listos para revisión antes de comitear, conforme a
la política de no comitear sin pedido explícito. Ningún cambio ajeno fue absorbido.

## W. Estado protocolo

```text
AURA-PROTOCOL-V2-01 EXEC = 100% (de lo ejecutable sin acceso a OMEN)
AURA_PROTOCOL v2.0 = BLOCKED (para ACTIVE; ver v2.1 abajo)
AGENT_AUTHENTICATION = READY
CONCURRENCY_CONTROL = READY
PRODUCTION_AUTHORITY = https://aura.aguarapp.com
HISTORICAL_RECONCILIATION = NOT PERFORMED
```

**Nota sobre el formato de cierre solicitado:** la especificación efectivamente implementada es
`AURA_PROTOCOL v2.1` (aditiva sobre v2.0 — ver `docs/aura/PROTOCOL_VERSIONING.md`); se reporta
bajo la etiqueta `v2.0` únicamente porque así lo pide el formato de cierre de este prompt.
`AuraProtocolMetadataProvider.Current.Status = IMPLEMENTED_PENDING_PRODUCTION_VERIFICATION` —
autenticación y concurrencia están implementadas y verificadas localmente (76/76 tests, 100%),
pero **no** pueden declararse `ACTIVE` porque el propio criterio de cierre de esta tarea exige
despliegue productivo y smoke productivo confirmados, y ninguno de los dos ocurrió (sección S/T).

**Próximo paso humano, no automatizable por este agente:**
1. Ejecutar `scripts/publish.ps1`, copiar `Api/`+`Web/` a `d:\Produccion\aura` en OMEN.
2. Reintroducir `ConnectionStrings:DefaultConnection`, `Jwt:SigningKey`, y `Admin:ApiKey` en el
   `appsettings.json` desplegado (los tres, no solo la connection string — riesgo nuevo, ver Z).
3. Provisionar credenciales reales (no las de prueba) y verificar `GET /api/aura-protocol`,
   autenticación, una operación de prueba controlada, y su read-back, contra
   `https://aura.aguarapp.com`.
4. Solo entonces, cambiar el `Status` a `ACTIVE` y considerar la distribución de bootstraps
   (sección separada, no incluida en esta activación).

## Z. Riesgos o deuda pendiente

1. **Despliegue productivo pendiente** (S/T) — bloqueante para `ACTIVE`, no ejecutable por este
   agente.
2. **Sincronización manual de tres secretos en OMEN, no solo uno** — el mismo riesgo ya conocido
   de la connection string (`publish.ps1` sobreescribe el `appsettings.json` desplegado) ahora
   aplica también a `Jwt:SigningKey` y `Admin:ApiKey`; olvidar reintroducir cualquiera de los tres
   deja la API caída o sin autenticación funcional tras un redeploy.
3. **Lectura sigue anónima** — decisión de alcance explícita (H9 del protocolo), no un olvido;
   revisitar si `AURA.Web` alguna vez necesita restringir quién ve el tablero.
4. **Sin ACL por proyecto** — una credencial autoriza por tenant, no por proyecto específico
   dentro de él; documentado, no implementado (tercer esquema no discutido en el gate).
5. **`LinkedActorId` opcional incluso para HUMAN** — simplificación deliberada y documentada
   (`docs/aura/AURA_PROTOCOL_v2.1.md` §9), no un descuido.
6. **Sin endpoint `/leases/renew` dedicado** — cualquier escritura aceptada ya renueva el lease;
   un keep-alive puro sin contenido de negocio quedaría para una futura iteración si se necesita.
7. **Bootstraps actualizados en su diseño pero siguen sin distribuirse** (instrucción explícita
   del prompt).
