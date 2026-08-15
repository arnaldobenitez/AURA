# Prompt para VS Code - AURA-INT-0002

## Encabezado

- **ID de intervención:** `AURA-INT-0002`
- **Etapa:** `ETAPA 1 - Base desplegable`
- **Título:** crear el esqueleto ejecutable de AURA
- **Fecha del prompt:** `2026-08-10 18:08:26 -03:00`
- **Estado inicial:** listo para codificación

## Rol del entorno de codificación

Actuá como desarrollador senior responsable de crear la primera base técnica ejecutable de AURA. Antes de modificar archivos, inspeccioná el repositorio completo y preservá todo contenido existente. Implementá exclusivamente el alcance indicado y no agregues funcionalidades de negocio todavía.

Trabajá directamente en:

```text
D:\Desarrollos\AURA
```

## Fuentes de verdad

Leé en este orden:

1. `docs/AURA-Development-Plan.md`
2. `docs/AURA-Project-Status.md`
3. `docs/AURA-Technical-Foundation.md`
4. `docs/AURA-VSCode-Prompt-Template.md`

`docs/Proyecto AURA.docx` conserva el requerimiento original. No lo modifiques. Ante una contradicción operativa, prevalece el plan de desarrollo actualizado.

## Contexto funcional

AURA será una plataforma SaaS multi-tenant para crear y gestionar proyectos basados en SCHEMA. El repositorio todavía no contiene la aplicación AURA: contiene documentación y el proyecto separado `Manual Aura`, que es el manual web de SCHEMA y no debe reutilizarse como aplicación transaccional.

Esta intervención inicia la ETAPA 1. Debe dejar una base backend/frontend mínima, compilable, comprobable y preparada para evolucionar hacia producción. No debe implementar todavía autenticación, tenants, proyectos ni EXEC.

Runtimes comprobados en el equipo:

- .NET SDK estable `10.0.201`;
- Node.js `22.20.0`;
- npm `10.9.3`.

## Objetivo

Crear el esqueleto ejecutable de AURA con solución .NET por capas, API con health check, aplicación React/TypeScript mínima y verificaciones automatizadas, sin depender todavía de una base de datos.

## Alcance incluido

### 1. Estructura raíz

Crear como mínimo:

```text
AURA.sln
global.json
Directory.Build.props
.editorconfig
.gitignore
README.md
src/
├── AURA.Domain/
├── AURA.Application/
├── AURA.Infrastructure/
├── AURA.Api/
└── AURA.Web/
tests/
├── AURA.UnitTests/
└── AURA.IntegrationTests/
scripts/
├── start-dev.ps1
└── verify.ps1
```

Usar `AURA.sln` en formato tradicional para máxima compatibilidad con Visual Studio y VS Code.

### 2. Configuración .NET

- Fijar SDK `10.0.201` en `global.json`, con roll-forward limitado a parches compatibles.
- Usar `net10.0` en todos los proyectos .NET.
- Activar `Nullable`, `ImplicitUsings` y análisis estático razonable.
- Tratar warnings del código propio como errores, evitando una configuración que rompa por warnings externos inevitables.
- Mantener dependencias por capas:
  - `Domain` no referencia otros proyectos;
  - `Application` referencia `Domain`;
  - `Infrastructure` referencia `Application` y `Domain`;
  - `Api` referencia `Application` e `Infrastructure`.
- No crear dependencias desde capas internas hacia `Api`.

### 3. API mínima

En `AURA.Api`:

- usar ASP.NET Core con controladores;
- habilitar `ProblemDetails` y un manejador global de excepciones;
- registrar health checks;
- exponer `GET /health` con HTTP 200 y JSON mínimo que incluya:
  - `status`;
  - `service` con valor `AURA.Api`;
  - `environment`;
  - timestamp UTC;
- exponer OpenAPI sólo en Development;
- usar logging estructurado mediante `ILogger` sin registrar secretos;
- agregar correlation ID por request, aceptando uno válido recibido o generando uno nuevo, y devolverlo en un header de respuesta;
- permitir CORS únicamente para orígenes loopback configurados en Development;
- no exigir SQL Server para iniciar la API en esta intervención;
- usar un puerto HTTP de desarrollo documentado y estable, preferentemente `5235`.

La respuesta de errores no debe exponer stack traces fuera de Development.

### 4. Frontend mínimo

Crear `src/AURA.Web` con Vite, React y TypeScript estricto:

- `package.json` con Node `>=22.20.0`;
- scripts `dev`, `build`, `lint` y `typecheck`;
- shell visual neutral con título AURA y leyenda “Adaptive Unified Reasoning Architecture”;
- una vista de estado que consulte `GET /health` de la API;
- estados visibles: comprobando, API disponible y API no disponible;
- configuración mediante `VITE_API_BASE_URL` documentada en `.env.example`;
- proxy de desarrollo o configuración equivalente para evitar hardcodear URLs productivas;
- HTML accesible, foco visible y diseño responsive básico;
- ningún componente, marca, esquema Drizzle ni código de negocio copiado desde `Manual Aura`;
- configuración para que el build estático pueda publicarse posteriormente en IIS con fallback SPA, sin realizar todavía una publicación real.

No agregar una librería global de estado si no es necesaria para esta pantalla.

### 5. Pruebas

- Crear tests unitarios mínimos para demostrar que la solución de tests funciona.
- Crear un test de integración de `GET /health` usando el host de pruebas de ASP.NET Core.
- El test debe comprobar HTTP 200, `service = AURA.Api` y presencia de timestamp.
- Dejar `Program` accesible al proyecto de integración de la forma estándar y mínima.
- No crear tests vacíos o que sólo afirmen `true` sin justificar el pipeline.

### 6. Scripts y documentación operativa

`scripts/start-dev.ps1` debe:

- iniciar API y frontend en modo desarrollo;
- usar procesos visibles o claramente identificables;
- no matar procesos ajenos ni puertos de forma agresiva;
- informar las URLs;
- propagar errores de arranque.

`scripts/verify.ps1` debe ejecutar en orden:

1. restore .NET;
2. build .NET;
3. tests .NET;
4. instalación reproducible del frontend;
5. typecheck;
6. lint;
7. build del frontend.

Actualizar `README.md` raíz con:

- propósito breve del repositorio;
- prerrequisitos exactos;
- estructura de proyectos;
- comandos para verificar y ejecutar;
- URLs locales;
- aclaración de que `Manual Aura` es documentación SCHEMA separada;
- aclaración de que la base de datos se incorporará en una intervención posterior;
- política de secretos: nunca versionar credenciales.

## Fuera de alcance

- autenticación, autorización y usuarios;
- Platform Owner, tenants, roles y permisos;
- entidades de proyecto, EXEC o métricas SCHEMA;
- Entity Framework, DbContext, migraciones o acceso a la base `AURA`;
- email, storage, sincronización cloud o agente local;
- apertura automática de VS Code;
- Docker, Kubernetes o CI/CD remoto;
- publicación en IIS;
- modificación de `Manual Aura`, `Metodologia ORCHESTRA` o documentos originales;
- actualización del porcentaje o del estado oficial del proyecto: eso lo hará ChatGPT después de revisar tu devolución.

## Requisitos de seguridad y compatibilidad

- No incorporar connection strings, passwords, tokens ni secretos.
- No copiar la credencial de desarrollo incluida en el documento original.
- No usar `TenantId = Guid.Empty` ni introducir decisiones de identidad prematuras.
- No habilitar CORS abierto global.
- No devolver detalles internos de excepción en Production.
- Usar timestamps UTC en la API.
- Evitar dependencias innecesarias o prerelease.
- No usar el SDK `10.0.100-rc.1`; usar `10.0.201` estable.
- Preservar archivos y cambios existentes.

## Criterios de aceptación

1. `dotnet restore AURA.sln` finaliza correctamente.
2. `dotnet build AURA.sln` finaliza sin errores.
3. `dotnet test AURA.sln` aprueba todos los tests, incluido el health check de integración.
4. La API inicia sin SQL Server y `GET /health` devuelve HTTP 200 con el contrato requerido.
5. El frontend instala dependencias de forma reproducible y aprueba `typecheck`, `lint` y `build`.
6. La pantalla muestra correctamente la disponibilidad o indisponibilidad de la API.
7. `scripts/verify.ps1` ejecuta toda la verificación y devuelve código distinto de cero ante un fallo.
8. `README.md` permite a otro desarrollador levantar el sistema sin contexto externo.
9. No se modifican archivos dentro de `Manual Aura` ni `Metodologia ORCHESTRA`.
10. No aparece ningún secreto en los archivos nuevos.

## Verificación obligatoria

Ejecutar y registrar los resultados de:

```powershell
dotnet --version
dotnet restore .\AURA.sln
dotnet build .\AURA.sln --no-restore
dotnet test .\AURA.sln --no-build
cd .\src\AURA.Web
npm ci
npm run typecheck
npm run lint
npm run build
```

Además, iniciar temporalmente la API y comprobar `GET /health`. Detener sólo el proceso iniciado para esa prueba.

Si alguna descarga de dependencias falla por red, informarlo como bloqueo y no ocultarlo. No reemplazar paquetes legítimos por código improvisado sólo para simular un build exitoso.

## Formato obligatorio de devolución

Responder exactamente con estas secciones:

1. **Estado:** completado, parcial o bloqueado.
2. **Resumen funcional.**
3. **Archivos creados/modificados.**
4. **Arquitectura resultante y referencias entre proyectos.**
5. **Configuración y variables de entorno.**
6. **Pruebas ejecutadas:** comando, resultado y cantidad de tests.
7. **Health check:** URL y respuesta observada.
8. **Criterios de aceptación:** lista del 1 al 10 con cumplido o pendiente.
9. **Decisiones y supuestos.**
10. **Riesgos, deuda o bloqueos.**
11. **Siguiente acción recomendada.**

No declares la intervención completada si falla el build, algún test obligatorio o el build del frontend. No avances a base de datos, autenticación ni multi-tenancy.
