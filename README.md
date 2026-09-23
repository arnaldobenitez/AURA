# AURA

AURA (Adaptive Unified Reasoning Architecture) es una plataforma SaaS multi-tenant para crear y gestionar proyectos basados en la metodología SCHEMA. Este repositorio contiene, en su estado actual, el esqueleto técnico ejecutable de AURA: una solución .NET por capas con una API mínima, persistencia SQL Server con migraciones EF Core y un frontend React/TypeScript mínimo, todavía sin autenticación, tenants ni EXEC.

## Prerrequisitos exactos

- .NET SDK `10.0.201` (fijado en `global.json`).
- Node.js `>=22.20.0`.
- npm `10.9.3` o compatible.
- PowerShell (los scripts en `scripts/` están escritos para Windows PowerShell / PowerShell 7).
- Acceso a una instancia SQL Server de desarrollo y una connection string válida para la base `AURA` (ver [Persistencia](#persistencia-sql-server)). No es necesaria para compilar, testear el backend con los tests por defecto, ni para levantar el frontend.

## Estructura de proyectos

```text
AURA.sln
global.json
Directory.Build.props
src/
├── AURA.Domain          # entidades y reglas de dominio (aún sin contenido de negocio)
├── AURA.Application      # casos de uso y contratos (aún sin contenido de negocio)
├── AURA.Infrastructure   # EF Core, SQL Server, AuraDbContext, migraciones
├── AURA.Api              # ASP.NET Core: controladores, health checks, middleware
└── AURA.Web              # React + TypeScript + Vite
tests/
├── AURA.UnitTests        # pruebas unitarias (.NET)
└── AURA.IntegrationTests # pruebas de integración con WebApplicationFactory
scripts/
├── start-dev.ps1                      # levanta API y frontend en modo desarrollo
├── verify.ps1                         # restore, build, tests y verificación del frontend (sin tocar la base)
├── database-update.ps1                # aplica migraciones EF Core sobre la base de desarrollo
├── database-verify.ps1                # verifica conexión y migraciones pendientes
├── project-control-schema-update.ps1  # aplica el esquema aditivo y siembra los proyectos del tenant 1
└── PersistenceCommon.ps1              # funciones compartidas por los scripts de base de datos
```

Dependencias entre proyectos .NET: `Domain` no referencia nada; `Application` referencia `Domain`; `Infrastructure` referencia `Application` y `Domain`; `Api` referencia `Application` e `Infrastructure`. Ninguna capa interna referencia `Api`.

## Cómo verificar el repositorio

```powershell
.\scripts\verify.ps1
```

Ejecuta en orden: `dotnet restore`, `dotnet build`, `dotnet test`, `npm ci`, `npm run typecheck`, `npm run lint` y `npm run build` sobre `src/AURA.Web`. Devuelve un código de salida distinto de cero ante cualquier fallo.

## Cómo ejecutar en desarrollo

```powershell
.\scripts\start-dev.ps1
```

Inicia la API y el frontend, cada uno en su propia ventana visible, e informa sus URLs. También pueden iniciarse por separado:

```powershell
dotnet run --project src/AURA.Api/AURA.Api.csproj --urls http://localhost:5235
```

```powershell
cd src/AURA.Web
npm install
npm run dev
```

## URLs locales

- API: `http://localhost:5235`
- Liveness (proceso activo, no depende de SQL Server): `http://localhost:5235/health` y `http://localhost:5235/health/live`
- Readiness (incluye conectividad SQL Server y estado de migraciones): `http://localhost:5235/health/ready`
- Control de proyectos IA (lectura): `http://localhost:5235/api/project-control?tenantId=1`
- Control de proyectos IA (registrar evento): `POST http://localhost:5235/api/project-control/events`
- OpenAPI (sólo en Development): `http://localhost:5235/openapi/v1.json`
- Frontend — Control de proyectos IA (pantalla principal, ruta `/`): `http://localhost:5173`

## Configuración

- `src/AURA.Web/.env.example` documenta `VITE_API_BASE_URL`. Copiarlo a `.env.local` y ajustarlo si la API no corre en `http://localhost:5235`. En desarrollo, el frontend también proxea `/health` hacia la API (ver `src/AURA.Web/vite.config.ts`), por lo que dejar `VITE_API_BASE_URL` vacío también funciona.
- CORS en la API sólo admite orígenes loopback configurados explícitamente en `src/AURA.Api/appsettings.Development.json` (`Cors:AllowedOrigins`), y únicamente en el entorno Development.

## `Manual Aura` y `Metodologia ORCHESTRA`

`Manual Aura/` es el manual web de la metodología SCHEMA: un producto documental independiente, con su propio repositorio Git. No es la aplicación transaccional AURA y no debe reutilizarse como tal. `Metodologia ORCHESTRA/` es documentación separada. Ninguna de las dos carpetas fue modificada por esta intervención.

## Persistencia (SQL Server)

`AURA.Infrastructure` incluye EF Core + SQL Server, un `AuraDbContext` (sin entidades de negocio todavía, en `Persistence/`) y una migración técnica inicial (`InitialInfrastructure`) que sólo crea `__EFMigrationsHistory`. La API **nunca migra ni crea la base automáticamente al arrancar**; aplicar migraciones es siempre un paso manual y separado (`database-update.ps1`).

### Configurar la connection string de desarrollo (sin versionarla)

La clave lógica en runtime es `ConnectionStrings:DefaultConnection`. Cargala con User Secrets del proyecto `AURA.Api` (nunca en `appsettings*.json`):

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<CONNECTION_STRING_DE_DESARROLLO>" --project src/AURA.Api
```

Si la API arranca sin esta configuración, `GET /health` y `GET /health/live` siguen respondiendo 200 (no dependen de SQL Server); `GET /health/ready` responde 503 de forma segura, sin revelar servidor, usuario ni contraseña.

### Variable de entorno para tooling de diseño (migraciones)

Las herramientas de EF Core (`dotnet ef migrations add|list`, `database update`) no leen User Secrets: necesitan la variable de entorno `AURA_DESIGNTIME_CONNSTR` con la misma connection string de desarrollo. La factory de diseño (`AuraDbContextFactory`) falla con un mensaje claro si no está definida; no tiene ningún valor de conexión por defecto.

```powershell
$env:AURA_DESIGNTIME_CONNSTR = "<CONNECTION_STRING_DE_DESARROLLO>"
```

`database-update.ps1` y `database-verify.ps1` usan esta variable si está definida y, si no, reutilizan automáticamente la de User Secrets de `AURA.Api`.

### Crear y aplicar migraciones

El repositorio fija la versión de `dotnet-ef` como herramienta local (`.config/dotnet-tools.json`); restaurala una vez con `dotnet tool restore`.

```powershell
# Crear una nueva migración (requiere AURA_DESIGNTIME_CONNSTR)
dotnet tool run dotnet-ef migrations add <Nombre> --project src/AURA.Infrastructure --startup-project src/AURA.Api --output-dir Persistence/Migrations

# Aplicar migraciones pendientes sobre la base de desarrollo 'AURA'
.\scripts\database-update.ps1

# Verificar conexión y migraciones pendientes
.\scripts\database-verify.ps1
```

Ambos scripts: nunca imprimen la connection string ni credenciales (la salida se sanitiza), nunca borran/recrean/resetean la base, y advierten (y por defecto abortan, salvo `-Force` en `database-update.ps1`) si la connection string configurada no apunta a una base llamada `AURA`.

`database-update.ps1` y `database-verify.ps1` son deliberadamente independientes de `verify.ps1`: `verify.ps1` no requiere ningún secreto ni conectividad a SQL Server y debe seguir funcionando en cualquier máquina, incluida una sin acceso a la base de desarrollo.

### Liveness vs. readiness

- `GET /health` y `GET /health/live`: liveness del proceso. Siempre 200 si la API está corriendo; no dependen de SQL Server.
- `GET /health/ready`: readiness. 200 sólo si SQL Server es accesible y no hay migraciones pendientes; 503 en cualquier otro caso (sin configurar, inaccesible, o con migraciones pendientes), sin exponer detalles de conexión.

### Resolución de errores sin revelar secretos

- **`database-update.ps1` o `database-verify.ps1` informan "no se encontró una connection string de desarrollo"**: definí `AURA_DESIGNTIME_CONNSTR` o cargá el secreto en `AURA.Api` con `dotnet user-secrets set` (ver arriba).
- **Advertencia "la connection string configurada no apunta a la base 'AURA'"**: revisá el valor cargado en User Secrets o en `AURA_DESIGNTIME_CONNSTR`; estos scripts no operan silenciosamente sobre otra base.
- **`GET /health/ready` devuelve 503**: sin más contexto en la respuesta (por diseño). Ejecutá `.\scripts\database-verify.ps1` en la misma máquina para diagnosticar si es un problema de configuración, de conectividad o de migraciones pendientes.
- **Herramientas de EF Core fallan por "Globalization Invariant Mode is not supported"**: `Directory.Build.props` ya desactiva `InvariantGlobalization` para toda la solución porque `Microsoft.Data.SqlClient` lo requiere; si el error reaparece, confirmá que no se reintrodujo esa propiedad en `true`.

## Control de proyectos IA

La ruta raíz (`/`) muestra **Control de proyectos IA**: una grilla con el estado de todos los proyectos del tenant 1 (`AURA`, `SiniTrax`, `Contadoro`, `TurnoMedix`, `Mandadini`, `Policursos`), construida directamente sobre las tablas heredadas `ProProyectosCabecera` / `ProProyectosItems` / `ProCodificadores` / `ProProyectosAnalistas` / `Usuarios` (no hay entidades de negocio nuevas en EF Core; el acceso es SQL directo desde `AURA.Infrastructure/ProjectControl`).

### Esquema

`ProProyectosCabecera` gana `sCodigo` (código estable, único por tenant) y `sRepositorio`. `ProProyectosItems` gana columnas de evento (`IdEvento`, `IdItemAnterior`, `nTipoEvento`, `nActorTipo`, `nEstadoTrabajo`, `nControlTipo`, `nControlId`, `sResumen`, `sProximaAccion`, `sBloqueo`, `sReferencia`, `sModelo`), todas nullable para no romper filas históricas. El actor de un evento reutiliza las columnas heredadas `nCodificador`/`nAnalista`/`nHumano` (una de las tres, según `nActorTipo`); no se agregó una columna de "actor id" separada. `sModelo` guarda el modelo/variante usado por el actor (p. ej. "Sonnet 5", "Fable") y es independiente del agente ejecutor; queda `NULL` para actores humanos.

`nHumano` es el humano que ejecutó el evento (cuando `nActorTipo = HUMAN`); `nControlId` con `nControlTipo = HUMAN` es el humano que debe continuar. Son roles distintos y nunca se pisan entre sí.

Aplicar el esquema (aditivo, idempotente; nunca borra ni recrea nada) y sembrar los 6 proyectos del tenant 1:

```powershell
.\scripts\project-control-schema-update.ps1
```

### Endpoints

- `GET /api/project-control?tenantId=1`: una fila por proyecto (incluso sin ítems todavía), con el último evento resuelto vía `OUTER APPLY`, el modelo usado (`lastModel`, `NULL` para actores humanos) y nombres de catálogo (incluyendo `Usuarios.sNombre` para actores/control humanos) con fallback (`Humano #<id>`, `Analista #<id>`, `Codificador #<id>`) cuando falta el registro. Requiere autenticación (ver abajo) desde el login básico agregado el 2026-08-28.
- `POST /api/project-control/events`: registra un evento nuevo. Requiere autenticación, es idempotente por `eventId` y usa `expectedPreviousItemId` como control optimista. `PROGRESS` de un Roadmap debe identificar `stageCode` y porcentaje. La aprobación formal de una etapa planificada requiere `AI_ANALYST` designado, `stageOutcome` y evidencia; `PASS_WITH_WARNINGS` cuenta como hito cerrado.
- `GET /api/project-control/projects/{projectCode}/roadmap?tenantId=1`: devuelve las etapas ordenadas, progreso individual, estado, validador asignado y última revisión.
- `PUT /api/project-control/projects/{projectCode}/roadmap`: define/actualiza el plan del EXEC vigente. Requiere credencial `HUMAN`, asigna una credencial activa `AI_ANALYST` a cada etapa y registra una revisión auditable.

### Autenticación, concurrencia y Roadmap de agentes (AURA_PROTOCOL v2.2)

`POST /api/project-control/events` y `POST /api/project-control/projects` exigen un JWT (`Authorization: Bearer <token>`), obtenido en `POST /api/auth/token` a partir de una credencial (`AgentCredential`: `credentialId` + secreto). Las credenciales se administran vía `/api/admin/agent-credentials` (crear/rotar/revocar/listar), protegido por el header `X-Admin-Api-Key`. Ver `docs/aura/AURA_PROTOCOL_v2.2.md` y `docs/aura/AGENT_INTEGRATION.md` para el detalle completo (claims del JWT, modelo de lease/concurrencia, códigos de respuesta).

### Login básico del tablero web (2026-08-28)

`AURA.Web` ahora exige iniciar sesión antes de mostrar cualquier cosa: `POST /api/auth/login` (`email` + `password`, tenant fijo en `1`) devuelve el mismo tipo de JWT que un `AgentCredential`, con `actorType = HUMAN`. Por eso `GET /api/project-control` y su historial también quedaron protegidos por `[Authorize]` — ya no son anónimos. Sin roles/permisos internos todavía: cualquier login válido ve todo igual (ver `docs/aura/AURA_PROTOCOL_v2.2.md` §2). `Usuarios` ganó `sEmail`/`sPasswordSalt`/`sPasswordHash`/`nPasswordIterations` (aditivo, nullable); el usuario `IdUsuario=1` del tenant `1` se siembra una sola vez con un login por defecto (nunca se pisa si ya tiene email configurado).

### Frontend

`src/AURA.Web/src/features/project-control/` contiene la pantalla: encabezado con indicador de salud compacto, tarjetas de resumen, y la grilla. Se actualiza sola al abrir, cada 30 s (pausada cuando la pestaña no está visible), y con el botón "Actualizar"; una falla de refresco muestra un aviso pero conserva los últimos datos válidos en pantalla. Sigue leyendo el tablero de forma anónima (no requiere login).

## Política de secretos

Nunca versionar credenciales, cadenas de conexión, claves ni tokens en este repositorio. La configuración sensible debe cargarse mediante variables de entorno o un secret store (por ejemplo, .NET User Secrets en desarrollo), nunca en `appsettings*.json` ni en archivos versionados. `.gitignore` excluye `.env*` (salvo `.env.example`) por la misma razón. `Jwt:SigningKey` y `Admin:ApiKey` siguen la misma convención que `ConnectionStrings:DefaultConnection`: User Secrets en desarrollo, editado a mano en el `appsettings.json` desplegado en producción — nunca comiteados en ningún `appsettings*.json`. Si falta configuración explícita y el entorno es `Development`, la API usa un placeholder fijo definido en código (nunca en un archivo versionado), explícitamente no apto para producción; fuera de `Development` la API rechaza arrancar sin configuración real.
test change
