# AURA — Technical Foundation

> Base tecnológica de **AURA (Adaptive Unified Reasoning Architecture)**, derivada mediante reverse engineering controlado de un proyecto externo identificado en este documento únicamente como `REF`. Fecha de inspección de la referencia: 2026-08-08. `REF` es exclusivamente evidencia técnica: este documento no propone clonarlo ni trasladar su dominio funcional. Describe el estado encontrado en su repositorio y no presupone que los artefactos locales ignorados por Git sean el despliegue vigente.

## 1. Resumen ejecutivo

REF es una aplicación web SaaS multi-tenant construida con una API ASP.NET Core orientada a controladores, cuatro proyectos .NET nominalmente separados (`Domain`, `Application`, `Infrastructure`, `Api`) y una SPA React/TypeScript independiente. La persistencia usa SQL Server mediante Entity Framework Core y un `DbContext` único. El frontend usa Vite, React Router, Axios, TanStack Query, Tailwind CSS y un kit UI propio. **AURA es el proyecto de destino y tendrá identidad, modelo y comportamiento propios.**

La base más valiosa para otro SaaS es:

- solución .NET por capas y API REST;
- SQL Server + EF Core + migraciones;
- resolución de tenant y ubicación operativa mediante middleware/proveedores;
- filtros globales EF por `TenantId`, sede y soft delete;
- JWT, roles, BCrypt, rate limiting de login y CORS por entorno;
- cliente Axios central con token, contexto operativo y reacción ante `401`;
- React Query, routing protegido, layout responsive y componentes UI propios;
- tokens visuales por variables CSS, temas y auditoría básica.

No debe interpretarse que REF ya ofrece un módulo de identidad completo. **No existen confirmación de email, SMTP, reenvío, recuperación de contraseña por email, refresh tokens, revocación, MFA ni autoservicio público de registro.** El alta actual es administrativa y habilita la cuenta de inmediato. Si AURA necesita esas capacidades, deben diseñarse expresamente; no pueden “extraerse” como código existente.

La arquitectura por capas es parcial: `Application` sólo contiene los contratos `ITenantProvider` e `ISedeProvider`; buena parte de los casos de uso y DTOs viven en controladores o servicios de `Api`, y muchos controladores acceden directamente a `ReferenceDbContext`. Conviene conservar la separación de proyectos, no copiar literalmente esa distribución interna.

Hay secretos de desarrollo versionados en `appsettings*.json` y un fallback de diseño dentro de `DesignTimeDbContextFactory.cs`. En este documento se representan como `[SECRETO CONFIGURADO — NO REPRODUCIDO]`. AURA debe usar Secret Manager/variables de entorno/secret store y no versionar valores reales.

**Fuentes:**

- `REF.sln`
- `REF.Api/Program.cs`
- `REF.Api/REF.Api.csproj`
- `REF.Infrastructure/Data/ReferenceDbContext.cs`
- `REF.Web/package.json`
- `REF.Web/src/App.tsx`
- `docs/REF-ETAPA-002-Seguridad-Identidad-Produccion.md`

## 2. Stack tecnológico

### Backend

| Recurso | Implementación comprobada | Evidencia |
|---|---|---|
| Lenguaje | C#; versión no fijada con `LangVersion` | Los cuatro `.csproj`; no hay `Directory.Build.props` |
| Versión de C# | **NO DETERMINADO DESDE EL REPOSITORIO**. El TFM es `net8.0`, pero no hay `global.json` ni `LangVersion` | Los cuatro `.csproj`; ausencia de `global.json` |
| Plataforma | .NET 8 (`net8.0`) | Los cuatro `.csproj` |
| Framework web | ASP.NET Core, proyecto Web SDK | `REF.Api/REF.Api.csproj` |
| Tipo de API | REST orientada a controladores con attribute routing | `Program.cs`; `Controllers/*.cs` |
| ORM | Entity Framework Core 9.0.10 | `.csproj`; `baseline.sql` registra `ProductVersion 9.0.10` |
| Proveedor de datos | `Microsoft.EntityFrameworkCore.SqlServer` 9.0.10 | proyectos `Api` e `Infrastructure` |
| Cliente SQL | `Microsoft.Data.SqlClient` 6.1.2 | `REF.Api.csproj` |
| DI | contenedor integrado de ASP.NET Core (`builder.Services`) | `Program.cs` |
| Serialización | `System.Text.Json`; enums como strings con `JsonStringEnumConverter` | `Program.cs` |
| Auth | JWT Bearer 8.0.10 + token propio HMAC-SHA256 | `.csproj`; `JwtTokenService.cs`; `Program.cs` |
| Hash de password | BCrypt.Net-Next 4.0.3 | `.csproj`; controladores de auth/usuarios |
| Logging | Serilog.AspNetCore 9.0.0, consola y archivo diario `logs/api-.log`; sink de archivo 7.0.0 | `.csproj`; `Program.cs` |
| OpenAPI | Swashbuckle 6.6.2; Swagger y UI sólo en Development | `.csproj`; `Program.cs` |
| Rate limiting | ASP.NET Core nativo, ventana fija | `Program.cs`; `AuthController.cs` |
| HTTP saliente | `HttpClient` registrado; también existe un `HttpClient` estático en seed | `Program.cs`; `SeedData.cs` |
| Health checks | No implementados | búsqueda global; `Program.cs` |
| Tests automatizados | No se encontró proyecto de tests | `REF.sln`; inventario de `.csproj` |

No hay AutoMapper, MediatR, FluentValidation, Dapper, Newtonsoft.Json, repositorio genérico ni Unit of Work propio.

### Frontend

Versiones resueltas desde `package-lock.json`, no sólo rangos de `package.json`:

| Grupo | Recurso | Versión/uso real |
|---|---|---|
| Estructural | React / React DOM | 19.2.0 / 19.2.0 |
| Estructural | TypeScript | 5.9.3; `strict`, `noEmit`, JSX `react-jsx`, target ES2022 |
| Estructural | Vite | 7.1.12; no existe `vite.config.*` en `REF.Web` |
| Estructural | React Router DOM | 7.13.0; data router con `createBrowserRouter` |
| Estructural | TanStack React Query | 5.90.6; server state, queries, mutations e invalidación |
| Estructural | Axios | 1.13.1; instancia e interceptores centrales |
| UI | Tailwind CSS / plugin PostCSS | 4.1.18 / 4.1.18 |
| UI | Font Awesome Free | 7.1.0 |
| UI | Kit de componentes | Propio, construido con React + clases Tailwind |
| Utilidad | date-fns | 4.1.0 |
| Utilidad UI | react-big-calendar | 1.19.4 |
| Utilidad UI | react-dnd / HTML5 backend | 16.0.1 / 16.0.1 |

No se encontraron React Hook Form, Formik, Zod, Yup, Redux, Zustand, MobX, Material UI, Bootstrap, AdminLTE, shadcn/ui, Radix UI, Lucide, Chakra o Ant Design como dependencias activas. `src/style.css` conserva comentarios de un baseline AdminLTE/Bootstrap, pero el entrypoint real es `src/main.tsx` y no importa ese archivo; tampoco hay paquetes correspondientes.

**Fuentes:**

- `REF.Web/package.json`
- `REF.Web/package-lock.json`
- `REF.Web/tsconfig.json`
- `REF.Web/index.html`
- `REF.Web/src/main.tsx`
- `REF.Web/src/App.tsx`

## 3. Arquitectura

### Estructura real simplificada

```text
personal-website/
├── REF.sln
├── REF.Domain/
│   ├── Entities/                  # entidades, base auditada y marcadores de scope
│   └── Enums/
├── REF.Application/
│   └── Abstractions/              # ITenantProvider, ISedeProvider
├── REF.Infrastructure/
│   ├── Data/
│   │   ├── ReferenceDbContext.cs
│   │   └── DesignTimeDbContextFactory.cs
│   └── Migrations/
├── REF.Api/
│   ├── Controllers/               # auth, owner, usuarios y dominio
│   ├── Middleware/                # resolución de tenant y sede
│   ├── Models/                    # algunos DTOs por módulo
│   ├── Services/                  # JWT, auditoría, seeds y servicios funcionales
│   ├── Properties/launchSettings.json
│   ├── Program.cs
│   └── appsettings*.json
├── REF.Web/
│   ├── public/                    # imágenes y web.config SPA para IIS
│   ├── src/
│   │   ├── components/
│   │   │   ├── layout-v2/         # AppShell, Sidebar, Topbar
│   │   │   └── ui/                # kit UI propio
│   │   ├── lib/                   # api, auth, tenant, sede, temas
│   │   ├── modules/               # módulos frontend agrupados
│   │   ├── pages/                 # rutas/pantallas
│   │   ├── styles/                # Tailwind + tokens visuales
│   │   ├── App.tsx
│   │   └── main.tsx
│   ├── package.json
│   ├── package-lock.json
│   ├── tailwind.config.ts
│   ├── postcss.config.cjs
│   └── tsconfig.json
├── docs/                          # auditorías, etapas y SQL operativo
├── baseline.sql
├── start-dev.ps1
└── s.ps1
```

### Dependencias entre proyectos

```text
Domain ← Application ← Infrastructure ← Api
  ↑           ↑               ↑          │
  └───────────┴───────────────┴──────────┘

Web ──HTTP/JSON──> Api
```

Detalle real:

- `Domain` no referencia otros proyectos.
- `Application` referencia `Domain`, pero hoy sólo aporta dos abstracciones de contexto.
- `Infrastructure` referencia `Application` y `Domain`, y aloja EF Core.
- `Api` referencia `Application` e `Infrastructure`; usa también entidades de `Domain` de forma transitiva.
- `Web` no está incluido en la `.sln`; es un proyecto npm separado.
- Los DTOs no siguen una única convención: hay records anidados en controladores, `Models/` en `Api` y tipos TypeScript junto a páginas/módulos.

El patrón efectivo es **API por controladores + EF Core directo**, con servicios especializados sólo en algunos módulos. No es Clean Architecture completa, CQRS ni arquitectura hexagonal estricta.

**Fuentes:** `REF.sln`, los cuatro `.csproj`, `Controllers/`, `Services/`, `REF.Web/src/`.

## 4. Backend

`Program.cs` configura:

1. Serilog a consola y archivos rotativos diarios.
2. Controladores y serialización de enums como strings.
3. Swagger/OpenAPI.
4. `ReferenceDbContext` con SQL Server.
5. CORS configurado por listas de orígenes/sufijos y loopback.
6. rate limiting por IP para login: 10 solicitudes por minuto, cola 0, respuesta `429`.
7. JWT Bearer con issuer, audience y clave simétrica.
8. proveedores scoped de tenant/sede, auditoría, limitador de plan y servicios funcionales.
9. migración/seed opcional al arrancar.
10. pipeline HTTPS redirection → CORS → rate limiter → authentication → authorization → middleware tenant → middleware sede → enriquecimiento Serilog → controllers.

Swagger sólo se publica en `Development`. No hay middleware global de excepciones ni `ProblemDetails`; los controladores suelen devolver strings o objetos anónimos. Tampoco existe health endpoint.

La configuración sensible de Production se valida de forma fail-fast: exige connection string y una clave JWT de al menos 32 caracteres que no parezca de desarrollo. Aun así, los archivos versionados contienen valores de desarrollo y deben considerarse deuda de seguridad.

**Fuentes:**

- `REF.Api/Program.cs`
- `REF.Api/REF.Api.csproj`
- `REF.Api/Services/*.cs`
- `REF.Api/Controllers/*.cs`
- `REF.Api/appsettings*.json`

## 5. Frontend

La SPA arranca desde `index.html` → `src/main.tsx` → `AppRouter`. `main.tsx` importa el CSS de temas, Tailwind y Font Awesome. `App.tsx` crea un `QueryClient`, un `ToastProvider` y el router.

### Routing

- `createBrowserRouter` con `RouterProvider`.
- `VITE_ROUTER_BASENAME` permite un subdirectorio; default `/`.
- Rutas tenant protegidas por `AuthGuard` y owner por `OwnerAuthGuard`.
- No existen rutas de registro público, confirmación de email, forgot password o reset password.
- Los guards verifican el store en un `useEffect` y mientras tanto renderizan `children`; no constituyen por sí solos una barrera de seguridad. La autorización real debe seguir en backend.

### Estado

- server state: TanStack Query;
- estado de formularios/pantallas: `useState`, `useMemo`, `useEffect` y validaciones manuales;
- sesión y sede: stores singleton propios persistidos en `localStorage`;
- temas: `localStorage` por usuario;
- no hay store global de terceros.

### Formularios y validación

Se usan inputs controlados y validaciones HTML (`required`, `type=email`) más chequeos manuales. No existe un esquema de validación compartido ni una librería de formularios. La validación de negocio se repite en frontend/backend según el caso.

### Restos de scaffold/legacy

`src/main.ts`, `src/counter.ts`, `src/style.css` y `src/components/Layout.tsx` no forman parte del entrypoint actual. No deben copiarse a la base neutral sin una verificación de uso.

**Fuentes:**

- `REF.Web/index.html`
- `REF.Web/src/main.tsx`
- `REF.Web/src/App.tsx`
- `REF.Web/src/components/*AuthGuard.tsx`
- `REF.Web/src/pages/Login.tsx`
- `REF.Web/src/pages/owner/OwnerLogin.tsx`

## 6. UI / UX Foundation

### Sistema comprobado

- **CSS:** Tailwind CSS 4 mediante PostCSS y Autoprefixer.
- **UI principal:** componentes propios; no shadcn/Radix/Material/Bootstrap.
- **Iconos:** Font Awesome Free mediante CSS global y clases `fas`, etc.
- **Tipografía:** stack del sistema: `ui-sans-serif`, `system-ui`, Segoe UI, Roboto, Helvetica, Arial.
- **Tokens:** variables CSS RGB `--tmx-bg`, `--tmx-surface`, `--tmx-text`, `--tmx-border`, `--tmx-accent`, `--tmx-success`, `--tmx-warning`, `--tmx-danger`.
- **Temas:** cinco paletas, aplicadas al elemento raíz y persistidas por usuario.
- **Loaders/estado:** `Spinner`, `Progress`, estados de carga en botones y feedback mediante `Toast`/`Alert`.
- **Gráficos:** no se encontró librería de charts; el componente visual especializado es `react-big-calendar` y se usa drag-and-drop en la agenda.
- **Dark mode:** no existe un modo oscuro comprobado; las cinco paletas son claras y no hay variantes `dark:`.
- **Responsive:** breakpoints Tailwind; sidebar overlay móvil y sidebar colapsable en desktop; grids adaptativos.

### Componentes reutilizables existentes

`Alert`, `Badge`, `Button`, `Card`, `Checkbox`, `Dialog`, `Dropdown`, `FormRow`, `Input`, `Progress`, `Select`, `Spinner`, `Switch`, `Table`, `Tabs`, `Textarea`, `Toast` y sus subcomponentes. Todos están en `src/components/ui` y se exportan desde `index.ts`.

El layout reusable incluye:

- `AppShell`: fondo, topbar sticky, sidebar, área principal y status bar opcional;
- `Sidebar`: grupos, navegación activa, colapso desktop y overlay móvil;
- `Topbar`: botón de menú, título y acciones;
- `AppLayout`: composición tenant, permisos de menú, selector de sede y logout;
- `OwnerLayout`: composición del portal owner.

`Dialog` usa portal, cierre por overlay/Escape y foco inicial, pero no implementa un focus trap completo. `Toast` usa contexto, portal y autocierre. `Table` es presentación HTML simple, sin sorting/paginación incorporados.

### Foundation neutral reutilizable

Se pueden reutilizar conceptualmente `AppShell`, `Sidebar`, `Topbar`, el kit `ui`, el helper `cn`, el sistema de tokens, Toast y patrones responsive. Antes de copiar:

- renombrar variables `--tmx-*`, keys de storage y textos de marca;
- reemplazar logos, títulos, menús y temas con semántica médica;
- eliminar el tema `teal-clinic` o neutralizar su nombre/descripción;
- desacoplar `AppLayout` de roles y permisos funcionales actuales;
- conservar sólo el entrypoint actual y descartar scaffold no usado;
- revisar accesibilidad de Dialog/Dropdown y agregar dark mode sólo si es requisito nuevo.

**Fuentes:**

- `REF.Web/tailwind.config.ts`
- `REF.Web/postcss.config.cjs`
- `REF.Web/src/styles/tailwind.css`
- `REF.Web/src/styles/theme.css`
- `REF.Web/src/lib/themes.ts`
- `REF.Web/src/components/ui/*`
- `REF.Web/src/components/layout-v2/*`

## 7. Base de datos

| Dato | Hallazgo |
|---|---|
| Motor | Microsoft SQL Server |
| Versión del servidor | **NO DETERMINADO DESDE EL REPOSITORIO** |
| Base | `REF` |
| Servidor configuración base | `26.65.32.68` |
| Servidor Development | `100.127.44.47` |
| Puerto | `51433` en ambos archivos |
| Autenticación | SQL authentication, usuario técnico `sa` |
| Password | `[SECRETO CONFIGURADO — NO REPRODUCIDO]` |
| Cifrado de transporte configurado | `Encrypt=False`; `TrustServerCertificate=True` |
| Proveedor EF | `Microsoft.EntityFrameworkCore.SqlServer` 9.0.10 |
| Connection string lógica | primero `DefaultConnection`, fallback `Default` |
| Ubicación | `REF.Api/appsettings.json` y `.Development.json` |
| Override de producción | `ConnectionStrings__DefaultConnection` |
| Override design-time | `REF_DESIGNTIME_CONNSTR` (alias documental del nombre real en la referencia) |

El repositorio también contiene `baseline.sql` y migraciones EF. Existen respaldos `.bak` fuera del repositorio fuente, en otra carpeta, pero no se inspeccionaron ni se usan como fuente de arquitectura.

La configuración actual expone direcciones, usuario técnico y secretos de desarrollo en archivos versionados. Para una base nueva: DB nueva, login de mínimo privilegio, `Encrypt=True`, secretos externos y rotación de los existentes.

**Fuentes:**

- `REF.Api/appsettings.json`
- `REF.Api/appsettings.Development.json`
- `REF.Api/Program.cs`
- `REF.Infrastructure/Data/DesignTimeDbContextFactory.cs`
- `baseline.sql`

## 8. Entity Framework y acceso a datos

### DbContext y patrón real

`ReferenceDbContext` recibe `DbContextOptions`, `ITenantProvider` e `ISedeProvider`. Declara un `DbSet` para entidades técnicas y de negocio y concentra las configuraciones Fluent API en un único `OnModelCreating` grande.

- No se encontraron Data Annotations de mapeo/validación en `Domain`.
- No hay repositorios ni Unit of Work explícitos; `DbContext`/`SaveChangesAsync` son la unidad de trabajo real.
- Muchos controladores acceden al contexto directamente.
- Algunos módulos usan interfaces/servicios especializados, por ejemplo financiadores y aranceles.
- El mapeo a DTOs es manual mediante LINQ, records y objetos anónimos.
- Hay transacciones explícitas sólo en operaciones puntuales complejas; el resto usa la transacción implícita de `SaveChanges`.

### Convenciones transversales

`BaseEntity` aporta `Id` GUID, `TenantId`, `CreatedAt/By`, `UpdatedAt/By` e `IsDeleted`. Otra base auditada exige `CreatedBy`. `ISedeScoped` marca entidades con `SedeId`.

En `OnModelCreating`, reflexión aplica filtros globales:

- entidades `BaseEntity`: `!IsDeleted && TenantId == tenant actual`;
- entidades además `ISedeScoped`: suma `SedeId == sede actual`;
- equivalentes para la base que exige `CreatedBy`;
- `Tenant` y `Plan` quedan fuera del filtro automático.

El sistema usa `IgnoreQueryFilters()` de forma explícita para login, owner, recuperación administrativa y búsquedas globales. Esa capacidad es necesaria, pero debe quedar encapsulada y auditada en una base nueva.

### Migraciones y actualización de esquema

Migraciones versionadas encontradas:

- `20260210213259_Baseline`
- `20260218030240_RQ28_TurnoCobroMetodoPagoArt`
- `20260220201552_RQ29_FacturacionBase`
- snapshot `ReferenceDbContextModelSnapshot.cs`

`SeedData.EnsureSeededAsync` ejecuta primero `Database.MigrateAsync()` y después bloques SQL idempotentes para hardening/drift y datos de referencia. La documentación interna reconoce drift entre snapshot EF y base real. Es una **estrategia híbrida**: migraciones EF + SQL idempotente en startup/manual.

En Development, el seed se ejecuta al arranque. En Production sólo se ejecuta si `RunSeedOnStartup=true`; los datos demo quedan bloqueados siempre en Production. Para AURA conviene adoptar una única estrategia de migraciones verificable y separar claramente migración, reference data y demo data.

**Fuentes:**

- `REF.Infrastructure/Data/ReferenceDbContext.cs`
- `REF.Infrastructure/Migrations/*`
- `REF.Api/Services/SeedData.cs`
- `REF.Domain/Entities/BaseEntity.cs`
- `REF.Domain/Entities/TenantAuditedEntityRequiredCreatedBy.cs`
- `REF.Domain/Entities/ISedeScoped.cs`
- `docs/REF-ETAPA-002-Seguridad-Identidad-Produccion.md`

## 9. Autenticación y autorización

### Login real

`POST /auth/login` recibe `{ email, password }`. Normaliza email con trim/lower, busca globalmente un usuario activo ignorando filtros de tenant/soft delete y verifica BCrypt. Usuario inexistente/inactivo y password inválido devuelven ambos `401` sin detalle al cliente; los logs sí distinguen la causa y registran el email.

Al aprobar, devuelve JWT y metadatos de sesión. No se usa ASP.NET Core Identity.

### JWT

- firma: HMAC-SHA256;
- issuer/audience actuales: `REF`;
- secreto: `[SECRETO CONFIGURADO — NO REPRODUCIDO]`;
- expiración: 8 horas desde creación;
- claims: `sub` (user id), `tenant`, role de .NET, `scope` (`owner`/`tenant`) y `jti` aleatorio;
- validación backend: issuer, audience y signing key; lifetime se rige por JWT bearer;
- refresh token: no existe;
- revocación/denylist: no existe;
- cookie de sesión: no existe;
- MFA/SSO: no existen.

### Roles y scopes

Roles enum actuales: `SuperAdmin`, `AccountAdmin`, `Professional`, `Assistant`. Son específicos en parte y no deben copiarse literalmente salvo los dos primeros si encajan con el nuevo producto. `scope=owner` se determina por `TenantId == Guid.Empty`; los usuarios tenant reciben `scope=tenant`.

La API usa `[Authorize]`, `[Authorize(Roles=...)]` y comprobaciones manuales de `scope`. El middleware valida además que el tenant siga activo. La autorización de owner se repite en controladores mediante chequeo de claim.

### Frontend y sesión

El token y metadatos se guardan como JSON en `localStorage` bajo `tmx_auth_v1`. Axios agrega `Authorization: Bearer`. Ante `401`, limpia sesión y sede y redirige a `/login` o `/owner/login`; ante `403` sólo rechaza la promesa, sin manejo global.

Logout tenant limpia `authStore` y `sedeStore`. El logout del `OwnerLayout` sólo llama al store legacy `ownerAuth` (`tmx_owner_auth_v1`) y **no limpia el JWT de `authStore`**; además `OwnerLogin` ya autentica con `authStore`. Esa inconsistencia no debe copiarse.

### Passwords

- hash BCrypt con defaults de la librería;
- política comprobada en endpoints administrativos: mínimo 6 caracteres;
- `register-tenant` de `AuthController` no aplica esa misma validación de longitud;
- no se comprobaron complejidad, historial, passwords comprometidos, expiración o lockout;
- cambio propio y cambio administrativo escriben un hash nuevo;
- no se exige password actual en `PUT /api/users/me/password`.

### Protecciones reales

- login: 10 intentos/minuto/IP, sin cola;
- email activo único global mediante índice filtrado y validación de aplicación;
- respuestas `401` iguales reducen enumeración por respuesta, aunque tiempo/logs pueden diferir;
- sin lockout persistente, captcha, MFA ni rate limit por cuenta;
- JWT en `localStorage` mantiene riesgo XSS;
- no hay revocación inmediata al desactivar/cambiar password; el middleware consulta tenant, pero no revalida usuario en todas las rutas antes de que expire el token.

**Fuentes:**

- `REF.Api/Controllers/AuthController.cs`
- `REF.Api/Controllers/UsersController.cs`
- `REF.Api/Services/JwtTokenService.cs`
- `REF.Api/Program.cs`
- `REF.Domain/Entities/User.cs`
- `REF.Domain/Enums/UserRole.cs`
- `REF.Web/src/lib/auth.ts`
- `REF.Web/src/lib/api.ts`
- `REF.Web/src/components/*AuthGuard.tsx`

## 10. Registro de usuarios

No hay registro público ni pantalla frontend de signup. Hay tres altas administrativas:

### A. Alta preferida de tenant y primer admin

`POST /owner/tenants`, protegido por JWT y comprobación `scope=owner`.

DTO `CreateTenantRequest`: `name`, `planId`, `cuit`, `email`, `adminPassword`, `direccion`, `pais`, `provincia`, `ciudad`, `isActive`.

Flujo real:

```text
Owner autenticado
→ valida email obligatorio y formato
→ valida password mínimo 6
→ valida plan
→ normaliza email y comprueba unicidad global de usuario no eliminado
→ crea Tenant (activo según request)
→ crea User SuperAdmin activo con BCrypt
→ relaciona PrimaryAdminUserId
→ guarda ambos
→ devuelve 201 con ids
```

### B. Alta alternativa de tenant

`POST /auth/register-tenant`, también `[Authorize]` y restringido manualmente a `scope=owner`. Recibe `TenantName`, `AdminEmail`, `AdminPassword`, crea/elige plan Starter, tenant activo y usuario `AccountAdmin` activo; devuelve inmediatamente un JWT tenant. Sólo exige email no vacío/único; no valida formato ni longitud de password en ese método.

### C. Alta dentro de un tenant

- `POST /api/users`: SuperAdmin/AccountAdmin crea usuario activo, proporciona password, mínimo 6, y se asigna la sede actual.
- `POST /auth/invite-user`: SuperAdmin/AccountAdmin crea usuario activo con password aleatorio de 8 caracteres. No envía email ni devuelve el password; sólo devuelve el id. Por tanto no es una invitación completa utilizable de forma autónoma.

En todos los flujos la cuenta queda activa inmediatamente. No se crea token de confirmación.

**Fuentes:**

- `REF.Api/Controllers/OwnerTenantsController.cs`
- `REF.Api/Controllers/AuthController.cs`
- `REF.Api/Controllers/UsersController.cs`
- `REF.Web/src/pages/owner/OwnerTenants.tsx`
- `REF.Web/src/pages/AdminUsers.tsx`

## 11. Validación de email

**No está implementada.**

| Elemento solicitado | Estado real |
|---|---|
| Token/código de verificación | No existe |
| Tabla/campos de verificación | No existen en `User`/`DbContext` |
| Hash de token | No existe |
| Expiración/un solo uso | No existe |
| Email de confirmación | No existe |
| Ruta frontend | No existe |
| Endpoint de confirmación | No existe |
| Reenvío | No existe |
| Rate limit de reenvío | No existe |
| Estado `EmailVerified` | No existe |

El email se valida sólo como identificador: algunos endpoints comprueban formato con `MailAddress`, se normaliza a minúsculas y existe unicidad global para usuarios no eliminados. El alta activa al usuario sin demostrar control del buzón.

Si AURA requiere validación de email, debe diseñarse un módulo nuevo con token aleatorio criptográfico, hash persistido, expiración, single-use, invalidación de tokens anteriores, respuesta anti-enumeración, rate limiting y auditoría. Eso es una recomendación nueva, no código disponible en REF.

**Fuentes:** `User.cs`, `AuthController.cs`, `OwnerTenantsController.cs`, `UsersController.cs`, `App.tsx`, búsqueda global de email/tokens/SMTP.

## 12. Recuperación de contraseña

**No existe recuperación de contraseña por email.** No hay `forgot-password`, `reset-password`, token, email, ruta frontend ni invalidación de sesión/token.

Sí existen operaciones administrativas distintas:

- `PUT /api/users/me/password`: usuario autenticado establece un password nuevo;
- `PUT /api/users/{id}/password`: admin autorizado establece password de otro usuario;
- `PUT /api/users/{id}/recover`: restaura un soft-deleted y, si estaba inactivo, obliga al admin a proveer password;
- `PUT /owner/tenants/{id}/admin-password`: owner cambia el password del admin primario.

Ninguna usa email ni token de un solo uso. La infraestructura futura puede compartir generador/almacén de tokens y `EmailService` con verificación de email, pero esa infraestructura debe crearse.

**Fuentes:** `REF.Api/Controllers/UsersController.cs`, `OwnerTenantsController.cs`, `REF.Web/src/App.tsx`.

## 13. Infraestructura de email

**No existe servicio de email transaccional en el repositorio.**

- librería SMTP/MailKit/SendGrid/etc.: no encontrada;
- interfaz/implementación `EmailService`: no encontrada;
- configuración SMTP: no encontrada;
- templates HTML: no encontrados;
- remitente/puerto/TLS: **NO DETERMINADO DESDE EL REPOSITORIO**;
- manejo de errores/log de envío: no existe.

`System.Net.Mail.MailAddress` se usa únicamente para validar sintaxis de email en algunos controladores; no envía correo.

Si AURA requiere email transaccional, debe definir como módulo independiente `IEmailSender`, implementación configurable, modelos de mensaje, templates, URL pública del frontend, timeouts/reintentos, observabilidad y pruebas. No hay infraestructura SMTP de REF para copiar.

**Fuentes:** `TenantController.cs`, `OwnerTenantsController.cs`, `.csproj`, `appsettings*.json`, búsqueda global.

## 14. Multi-tenancy

REF sí implementa multi-tenancy y un segundo scope operativo por sede.

### Infraestructura genérica

- `Tenant` y `Plan`;
- `TenantId` en entidades basadas en `BaseEntity`;
- claim JWT `tenant` y claim `scope`;
- `TenantResolutionMiddleware` valida autenticación, scope, GUID y tenant activo, y guarda el id en `HttpContext.Items`;
- `ITenantProvider`/`TenantProvider` exponen el id a EF;
- filtros globales EF por tenant y soft delete;
- `IgnoreQueryFilters` para owner/login/administración global;
- auditoría con `TenantId`, usuario, acción, entidad y JSON;
- planes con límites y `PlanLimiter`.

### Implementación específica o acoplada

- `Sede`, `UserSede`, `ISedeScoped`, `SedeResolutionMiddleware` y header `X-Sede-Id` representan una ubicación/unidad operativa. El patrón es reusable si el nuevo dominio requiere sucursales/workspaces, pero los nombres deben neutralizarse.
- Roles `Professional`/`Assistant`, flags de usuario, límites de pacientes/turnos y varias propiedades de `Tenant` son específicos.
- El owner se representa con `TenantId = Guid.Empty`; es funcional, pero una base neutral debería modelar identidad de plataforma de forma explícita.

### Aislamiento efectivo

El tenant **no llega en un header**: se deriva del JWT. Los múltiples mensajes “Falta X-Tenant” están desactualizados. El frontend sólo agrega `X-Sede-Id` para el segundo scope.

El aislamiento combina middleware, filtros EF y filtros manuales. Es sólido como defensa en profundidad, pero `IgnoreQueryFilters` y las entidades excluidas (`Tenant`, `Plan`) requieren revisión estricta. No existe una relación muchos-a-muchos usuario-organización: cada usuario tiene un único `TenantId`; owner usa GUID vacío.

**Fuentes:**

- `TenantResolutionMiddleware.cs`
- `SedeResolutionMiddleware.cs`
- `TenantProvider.cs`, `SedeProvider.cs`
- `ITenantProvider.cs`, `ISedeProvider.cs`
- `ReferenceDbContext.cs`
- `BaseEntity.cs`, `Tenant.cs`, `Sede.cs`, `UserSede.cs`
- `REF.Web/src/lib/sede.ts`
- `REF.Web/src/lib/api.ts`

## 15. Comunicación Frontend/API

### Configuración

- cliente: Axios singleton `api`;
- `baseURL`: `VITE_API_BASE` o `/api`;
- ejemplo versionado: `VITE_API_BASE=http://localhost:5232`;
- `start-dev.ps1` fija el mismo valor con el puerto elegido;
- no hay `vite.config.*` ni proxy Vite;
- basename router: `VITE_ROUTER_BASENAME` o `/`.

### Headers e interceptores

- request: `Authorization: Bearer {token}` si hay sesión;
- request: `X-Sede-Id` si hay sede, excepto `/api/me`;
- no se envía `X-Tenant`; el tenant se toma del JWT;
- response `401`: limpia sesión/sede y redirige;
- response `403`: sin tratamiento central;
- demás errores: se propagan y cada pantalla decide cómo mostrarlos.

### CORS

La API permite headers/métodos, credenciales y expone `Content-Disposition`. Orígenes exactos, sufijos y loopback son configurables. Development permite loopback y sufijos `aguarapp.com`, `trycloudflare.com`, `ngrok-free.app`; Production no permite origen externo salvo configuración explícita.

### URLs y despliegue

El frontend mezcla endpoints `/auth`, `/owner` y `/api`. Con base absoluta en desarrollo funciona contra la raíz del backend. Para un hosting bajo subaplicación `/api`, el valor productivo de `VITE_API_BASE` debe probarse cuidadosamente para evitar prefijos duplicados en llamadas que ya comienzan por `/api`. No hay `.env.production` versionado que establezca el valor definitivo.

**Fuentes:**

- `REF.Web/src/lib/api.ts`
- `REF.Web/.env.local.example`
- `REF.Web/src/App.tsx`
- `start-dev.ps1`
- `REF.Api/Program.cs`
- `REF.Api/appsettings.Development.json`

## 16. Desarrollo local

### Prerrequisitos comprobables

- Windows/PowerShell para los scripts existentes;
- SDK .NET compatible con `net8.0`;
- Node.js/npm compatibles con las dependencias (versión mínima no fijada: **NO DETERMINADO DESDE EL REPOSITORIO**);
- conectividad al SQL Server configurado y credenciales válidas;
- permisos para migrar/escribir la base si corre el seed;
- acceso saliente a fuentes geográficas si el seed necesita completarlas.

La solución declara Visual Studio 17 en `.sln` y perfiles IIS Express, por lo que Visual Studio está soportado. VS Code no está documentado: **NO DETERMINADO DESDE EL REPOSITORIO**.

### Puertos/perfiles

- API HTTP: `5232`;
- API HTTPS: `7160` y HTTP `5232` en perfil `https`;
- IIS Express: HTTP `28875`, SSL `44328`;
- Vite default propio: normalmente 5173, pero `start-dev.ps1` fuerza `5180`;
- script conjunto usa HTTP para API/frontend; `UseHttpsRedirection` está activo, por lo que el comportamiento depende del entorno/perfil.

### Cómo levantar REF desde cero

1. Abrir terminal en la raíz real del repositorio.
2. Verificar que la connection string de Development apunte a un SQL Server autorizado. No reutilizarla para otro proyecto.
3. Restaurar/compilar backend con `dotnet restore REF.sln` y `dotnet build REF.sln` (el script también puede compilar al ejecutar).
4. Ejecutar `./start-dev.ps1` para el flujo integrado. El script:
   - libera los puertos elegidos;
   - inicia `dotnet run` en `http://localhost:5232`;
   - espera a que escuche;
   - define `VITE_API_BASE`;
   - ejecuta `npm ci` si falta `node_modules`;
   - ejecuta `npx tsc --noEmit`;
   - inicia Vite en `http://localhost:5180`.
5. En Development, la API ejecuta `Database.MigrateAsync`, SQL idempotente y datos demo/reference según configuración.
6. Alternativa API sola: `./s.ps1` o `dotnet run --project REF.Api --urls http://localhost:5232`.
7. Alternativa frontend sola: dentro de `REF.Web`, establecer `VITE_API_BASE`, ejecutar `npm ci` y `npm run dev`.

No se ejecutaron estos pasos durante esta tarea documental para evitar modificar base de datos o procesos.

**Fuentes:** `start-dev.ps1` (con cambio local previo del usuario), `s.ps1`, `launchSettings.json`, `package.json`, `Program.cs`, `.sln`.

## 17. Producción y despliegue

### Evidencia versionada

- `REF.Web/public/web.config` configura IIS URL Rewrite para fallback SPA a `index.html`, excluyendo `/api`, y MIME types WebP/WOFF2.
- `Program.cs` tiene comportamiento Production y secretos vía variables de entorno.
- `.gitignore` excluye `publish/`, `_publish/`, `artifacts/` y `dist/`.
- no se encontraron Dockerfile, pipeline CI/CD, workflow ni perfil `.pubxml` versionados.

Hay artefactos locales ignorados que contienen una publicación de API y un `web.config` generado para `AspNetCoreModuleV2`, hosting in-process y `REF.Api.dll`. Esto demuestra que se generó al menos una publicación compatible con IIS, pero no prueba el entorno actualmente desplegado ni constituye configuración versionada.

| Dato | Estado |
|---|---|
| Tecnología prevista | IIS para SPA; API compatible con ASP.NET Core Module |
| Servidor real/hostname | **NO DETERMINADO DESDE EL REPOSITORIO** |
| Windows Server y versión | **NO DETERMINADO DESDE EL REPOSITORIO** |
| Dominio productivo | **NO DETERMINADO DESDE EL REPOSITORIO** |
| Ruta/subdirectorio definitivo | **NO DETERMINADO DESDE EL REPOSITORIO** |
| HTTPS/certificado | **NO DETERMINADO DESDE EL REPOSITORIO** |
| Reverse proxy adicional | **NO DETERMINADO DESDE EL REPOSITORIO** |
| App pool/identidad/permisos | **NO DETERMINADO DESDE EL REPOSITORIO** |
| CI/CD | No existe configuración versionada |
| Base productiva real | **NO DETERMINADO DESDE EL REPOSITORIO**; `appsettings.json` contiene una conexión base, no prueba uso productivo |
| Estrategia schema Production | `RunSeedOnStartup` opcional; documentación menciona SQL manual para entornos sin seed |

Variables previstas: `ConnectionStrings__DefaultConnection`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `Cors__AllowedOrigins__N`, `RunSeedOnStartup`, `SeedDemoData` (forzado false en Production) y, para tooling, `REF_DESIGNTIME_CONNSTR` (alias documental).

**Fuentes:** `REF.Web/public/web.config`, `.gitignore`, `Program.cs`, `docs/REF-ETAPA-002-Seguridad-Identidad-Produccion.md`; artefacto local ignorado `publish/api/web.config` sólo como indicio.

## 18. Componentes reutilizables

### A. REUTILIZAR

- estructura de solución Domain/Application/Infrastructure/Api + Web;
- ASP.NET Core controllers, DI y configuración por entorno;
- SQL Server, EF Core, migraciones y factory design-time sin fallback secreto;
- `BaseEntity` neutral, soft delete, timestamps y auditoría;
- patrón `TenantProvider` + middleware + filtros globales;
- concepto de segundo scope operativo sólo si el nuevo producto lo necesita;
- JWT Bearer, BCrypt, roles/policies y rate limiting como punto de partida;
- Serilog a consola/archivo, mejorando configuración y redacción de PII;
- CORS configurable y validación fail-fast Production;
- Axios central, React Query, routing, guard, layout y feedback UI;
- kit `components/ui`, tokens visuales, temas y responsive;
- `AuditLogger`, generalizado y desacoplado del `DbContext` de API.

### B. ADAPTAR

- `User`: quitar flags funcionales y separar identidad/perfil;
- `Tenant`: neutralizar propiedades y límites funcionales;
- `Plan`/`PlanLimiter`: conservar patrón, reemplazar métricas médicas;
- roles: reemplazar `Professional`/`Assistant` por roles/permisos neutrales;
- `Sede`/`UserSede`: renombrar a Workspace/Branch/Location si aplica;
- `AppLayout`/`OwnerLayout`: conservar shell, regenerar menús y logout;
- `authStore`: eliminar prefijo/marca, unificar owner/tenant y revisar almacenamiento seguro;
- `themes.ts`: conservar mecanismo, no nombres/descripciones/logos médicos;
- estrategia de seed/migración: separar y corregir drift;
- DTO/API error handling: centralizar en Application y `ProblemDetails`;
- componentes UI: mejorar accesibilidad antes de declararlos design system.

### Riesgos que no deben heredarse

- secrets versionados y fallback de conexión con secreto;
- password mínimo 6 y políticas inconsistentes;
- JWT sin refresh/revocación en `localStorage`;
- logout owner incompleto y doble store de sesión;
- guards que renderizan antes de redirigir;
- `IgnoreQueryFilters` disperso;
- `DbContext` central gigante y controladores con demasiada lógica;
- migraciones + SQL startup con drift;
- textos “X-Tenant” que no reflejan el mecanismo real;
- demo credentials precargadas en pantallas de login;
- logs con email de cada intento;
- ausencia de health checks, tests y pipeline.

**Fuentes:** archivos citados en secciones 3–17.

## 19. Componentes que NO deben reutilizarse

No copiar entidades, tablas, endpoints, DTOs, páginas, servicios, reglas, seeds, iconografía ni textos correspondientes a:

- pacientes, fichas, antecedentes, evoluciones, estudios y adjuntos clínicos;
- profesionales, asistentes, especialidades y prestaciones;
- turnos, agenda, reprogramación, operaciones diarias y consultorios médicos;
- obras sociales, financiadores, convenios, aranceles, cobros y facturación médica/fiscal específica;
- límites de planes basados en pacientes/turnos/storage actual;
- roles/flags `Professional`, `Assistant`, monitor de turnos, aranceles y alquiler de consultorio;
- datos demo, nomencladores y scripts SQL funcionales;
- assets y nombres REF.

`Sede` es una zona gris: el mecanismo de scope es técnico y reusable; la entidad concreta, sus campos y asociaciones actuales deben adaptarse, no copiarse.

**Fuentes:** `REF.Domain/Entities`, `Enums`, `Api/Controllers`, `Api/Services`, `Web/src/pages`, `Web/src/modules`, `SeedData.cs`, migraciones.

## 20. Propuesta de base técnica neutral para AURA

No desarrollar todavía; arquitectura objetivo conceptual:

```text
AURA.sln
├── AURA.Domain/
│   ├── Identity/          # User, Role, Permission
│   ├── Tenancy/           # Organization/Tenant, Membership
│   ├── Auditing/
│   └── Common/            # Entity, soft delete, timestamps
├── AURA.Application/
│   ├── Abstractions/      # tenant, clock, email, tokens, current user
│   ├── Authentication/    # login, refresh, logout/revoke
│   ├── Registration/      # register/invite/verify/resend
│   ├── Passwords/         # forgot/reset/change
│   └── Users/
├── AURA.Infrastructure/
│   ├── Persistence/       # DbContext, configurations, migrations
│   ├── Identity/          # BCrypt/PasswordHasher, JWT, token hashing
│   ├── Email/             # provider + templates
│   └── Observability/
├── AURA.Api/
│   ├── Controllers/
│   ├── Middleware/
│   └── Program.cs
└── AURA.Web/
    └── src/
        ├── app/           # router/providers
        ├── auth/          # login/register/verify/reset/guards
        ├── components/ui/
        ├── components/layout/
        ├── lib/api/
        ├── tenancy/
        └── styles/
```

Decisiones recomendadas basadas en hallazgos:

- conservar los cinco proyectos, pero mover casos de uso y contratos reales a `Application`;
- configuraciones EF por entidad, no un único `OnModelCreating` monolítico;
- tenant explícito y membresía usuario-organización; no owner mediante GUID vacío;
- permissions/policies neutrales; roles como agrupadores;
- identidad completa: Register, VerifyEmail, ResendVerification, Login, Refresh, Logout/Revoke, ForgotPassword, ResetPassword, ChangePassword;
- tokens opacos aleatorios, sólo hash persistido, expiración y uso único;
- `IEmailSender` desacoplado con templates;
- API errors con `ProblemDetails`, exception handler, correlation id y health checks;
- migraciones controladas fuera del startup productivo salvo decisión explícita;
- sesión frontend unificada, política consciente para token (BFF/cookie HttpOnly preferible si la arquitectura lo permite; si se conserva localStorage, CSP y mitigación XSS);
- kit UI neutral conservando tokens y componentes propios;
- pruebas de aislamiento tenant, auth/token, email y componentes críticos.

Esta propuesta toma decisiones técnicas observadas en REF como referencia. AURA no hereda su marca, dominio, entidades, endpoints, tablas, roles funcionales ni reglas de negocio.

## 21. Tablas técnicas mínimas

AURA debe usar **su propia base de datos**. Mantener SQL Server como motor es razonable si responde a sus requisitos; ninguna tabla funcional de REF debe migrarse.

### Núcleo mínimo recomendado

| Tabla | Propósito | Relación con REF |
|---|---|---|
| `Organizations` / `Tenants` | tenant neutral, estado y metadatos mínimos | Adaptar `Tenants` |
| `Users` | identidad global, email normalizado, hash, estado, email verificado | Adaptar fuertemente `Users` |
| `OrganizationMemberships` | relación usuario↔tenant y estado | Nueva; evita un solo `TenantId` rígido |
| `Roles` | roles neutrales configurables | Nueva; REF usa enum |
| `Permissions` | permisos atómicos | Nueva |
| `RolePermissions` | permisos por rol | Nueva |
| `MembershipRoles` | roles del usuario dentro del tenant | Nueva |
| `EmailVerificationTokens` | hash, expiración, consumo, usuario, auditoría | Nueva |
| `PasswordResetTokens` | hash, expiración, consumo, usuario | Nueva |
| `RefreshTokens` / `Sessions` | sesión rotativa, revocación, dispositivo | Nueva; sólo si se adopta refresh |
| `AuditLogs` | actor, tenant, acción, entidad, datos/correlation id | Reutilizar concepto |

### Opcionales según alcance

- `Invitations` para alta por invitación;
- `Locations` y `MembershipLocations` si se necesita segundo scope;
- `Plans`, `Subscriptions`, `PlanFeatures` si la plantilla incluirá billing/entitlements;
- `OutboxMessages`/`EmailDeliveries` para email fiable y trazable;
- `ApiKeys` si habrá integraciones máquina-a-máquina.

Convenciones: claves GUID/UUID; timestamps UTC; soft delete sólo donde aporte valor; índices únicos sobre email normalizado y tokens hash; `TenantId`/`OrganizationId` en toda tabla tenant-scoped; claves foráneas e índices compuestos que incluyan el tenant; nunca almacenar tokens de verificación/reset en claro.

**Fuente de comparación:** `BaseEntity.cs`, `User.cs`, `Tenant.cs`, `AuditLog.cs`, `ReferenceDbContext.cs`. Las tablas de tokens/permisos/membresías son propuesta nueva porque no existen actualmente.

## 22. Matriz de reutilización

| Recurso | REF usa | AURA debería usar | Reutilización |
|---|---|---|---|
| Backend | ASP.NET Core controller API | ASP.NET Core con capas reales | Mantener/adaptar |
| Framework | .NET 8 | .NET soportado decidido al iniciar; .NET 8 replica la base | Mantener si sigue soportado |
| Lenguaje | C#, versión no fijada | C# fijado por SDK/global.json | Adaptar gobernanza |
| Base de datos | SQL Server, DB `REF` | SQL Server, DB nueva | Mismo motor / nueva DB |
| ORM | EF Core 9.0.10 | EF Core compatible con TFM elegido | Mantener patrón |
| Migraciones | EF + SQL idempotente híbrido | Migraciones coherentes y automatizadas | Adaptar, no copiar drift |
| DI | ASP.NET Core built-in | Igual | Mantener |
| Serialización | System.Text.Json, enums string | Igual con contrato explícito | Mantener |
| Logging | Serilog consola/archivo | Serilog/observabilidad configurada | Mantener/adaptar |
| OpenAPI | Swagger sólo Development | OpenAPI con política por entorno | Mantener |
| Health checks | No | Sí | Agregar |
| Frontend | React 19 + TypeScript + Vite | Igual si no hay nuevo condicionante | Mantener |
| Routing | React Router 7 | Igual | Mantener |
| Server state | TanStack Query 5 | Igual | Mantener |
| API client | Axios + interceptores | Cliente central tipado | Reutilizar arquitectura |
| UI | Tailwind 4 + componentes propios | Foundation neutral propia | Mantener/adaptar |
| Iconos | Font Awesome Free 7 | Igual o decisión nueva | Mantener posible |
| Fechas | date-fns 4 | Igual | Mantener |
| Auth | JWT custom + BCrypt | Módulo neutral endurecido | Reutilizar concepto, rediseñar sesión |
| Roles | enum fijo | roles/permisos neutrales | Adaptar |
| Validación email | No existe | Sí, token hash/expiry/single-use | Crear |
| Recuperación password | Sólo cambios administrativos | Forgot/reset por email seguro | Crear |
| SMTP/email | No existe | `IEmailSender` + proveedor/config/templates | Crear/reconfigurar |
| Multi-tenancy | `TenantId`, middleware, filtros EF | tenant + memberships + policies | Reutilizar arquitectura/adaptar modelo |
| Segundo scope | Sede + `X-Sede-Id` | Sólo si el dominio lo requiere | Adaptar/opcional |
| Auditoría | `AuditLog` + servicio básico | Auditoría central y correlation id | Reutilizar/adaptar |
| Deploy | Evidencia de IIS/SPA rewrite | Pipeline reproducible y config externa | Adaptar; entorno real no determinado |
| CI/CD | No versionado | Sí | Crear |
| Tests | No proyecto encontrado | Tests de auth/tenant/API/UI | Crear |

## 23. Archivos fuente inspeccionados

Inventario y lectura directa, agrupados por función:

- solución/proyectos: `REF.sln`, los cuatro `.csproj`, `.gitignore`;
- arranque/config: `Program.cs`, `appsettings.json`, `appsettings.Development.json`, `launchSettings.json`, `start-dev.ps1`, `s.ps1`;
- datos: `ReferenceDbContext.cs`, `DesignTimeDbContextFactory.cs`, migraciones y snapshot, `baseline.sql`, `SeedData.cs`;
- modelo técnico: `BaseEntity.cs`, `TenantAuditedEntityRequiredCreatedBy.cs`, `ISedeScoped.cs`, `Tenant.cs`, `Plan.cs`, `User.cs`, `UserSede.cs`, `Sede.cs`, `AuditLog.cs`, `UserRole.cs`;
- identidad: `AuthController.cs`, `UsersController.cs`, `OwnerTenantsController.cs`, `MeController.cs`, `TenantController.cs`, `JwtTokenService.cs`, middlewares y providers de tenant/sede, `AuditLogger.cs`;
- búsquedas globales en todos los `.cs/.csproj` para email, SMTP, confirmación, reset, refresh, repositorios, mappers, validadores, health checks, transacciones y autorización;
- frontend base: `package.json`, `package-lock.json`, `tsconfig.json`, `tailwind.config.ts`, `postcss.config.cjs`, `.env.local.example`, `index.html`, `main.tsx`, `App.tsx`;
- frontend auth/API: `api.ts`, `auth.ts`, `ownerAuth.ts`, `sede.ts`, guards, páginas Login/OwnerLogin, layouts owner/tenant;
- UI: `styles/*`, `themes.ts`, todo `components/ui`, `components/layout-v2`, `AppLayout`, `SedeSelector`;
- búsquedas globales en `src` para dependencias UI, estado, formularios, responsive, dark mode, variables de entorno y llamadas API;
- despliegue/documentación: `public/web.config`, documentos ETAPA 000–004, SQL de diagnóstico/aplicación y `BITACORA.md`;
- artefactos locales ignorados: sólo inventariados para distinguir indicios de publicación de fuentes versionadas; no tratados como verdad productiva.

No se consultó la base viva, no se abrieron backups, no se ejecutaron migraciones/seed y no se inició la aplicación.

## 24. Datos no determinados

Los siguientes datos quedan expresamente como **NO DETERMINADO DESDE EL REPOSITORIO**:

- versión exacta de C# efectiva y SDK .NET usado por CI/producción (no hay `global.json`/`LangVersion`);
- versión de SQL Server;
- cuál de las conexiones versionadas corresponde al entorno productivo real;
- servidor, sistema operativo y versión de IIS reales;
- dominio, DNS, certificado HTTPS, ruta de aplicación y reverse proxy definitivos;
- identidad/permisos del app pool y estrategia real de backups/HA;
- comandos/proceso de publicación repetible y CI/CD;
- variables/secret store efectivamente configurados fuera del repositorio;
- versión mínima de Node/npm soportada (no hay `engines`, `.nvmrc` ni `.node-version`);
- uso formal de VS Code;
- proveedor SMTP, remitente, puerto, TLS y templates, porque el servicio no existe;
- política operativa de rotación/revocación JWT fuera del código;
- monitoring/APM centralizado, SIEM y retención real de logs;
- SLA, observabilidad productiva y disaster recovery.

La ausencia de evidencia no debe completarse por convención. Cualquier IA que use este documento debe mantener estos puntos como desconocidos hasta obtener configuración externa o evidencia operativa.
