# Prompt para VS Code - AURA-INT-0004

## Encabezado

- **ID de intervención:** `AURA-INT-0004`
- **Etapa:** `ETAPA 1 - Base desplegable`
- **Título:** incorporar persistencia SQL Server y migración inicial segura
- **Fecha del prompt:** `2026-08-11 18:58:04 -03:00`
- **Estado inicial:** listo para codificación

## Rol del entorno de codificación

Actuá como desarrollador senior responsable de incorporar la fundación de persistencia de AURA. Inspeccioná primero el árbol actual y preservá la base verificada en AURA-INT-0003. Implementá únicamente infraestructura SQL Server, migración inicial, readiness y sus pruebas/scripts; no avances a autenticación ni al modelo SaaS.

Trabajá directamente en:

```text
D:\Desarrollos\AURA
```

## Fuentes de verdad

Leé en este orden:

1. `docs/AURA-Development-Plan.md`
2. `docs/AURA-Project-Status.md`
3. `docs/reviews/AURA-INT-0003-Review.md`
4. `docs/AURA-Technical-Foundation.md`
5. `README.md`

No modifiques `docs/Proyecto AURA.docx`, `Manual Aura` ni `Metodologia ORCHESTRA`.

## Contexto funcional

AURA-INT-0002 dejó una solución .NET 10 por capas, API y frontend verificados. La API inicia sin base y expone `GET /health`. La ETAPA 1 todavía requiere demostrar persistencia SQL Server y una migración reproducible.

La base de desarrollo será `AURA` en la instancia SQL Server indicada en el documento rector. La connection string contiene una credencial de desarrollo: podés usarla para la verificación local, pero sólo mediante .NET User Secrets o variables de entorno. No la copies a archivos versionados, logs, respuestas ni documentación.

## Objetivo

Incorporar Entity Framework Core con SQL Server, un `AuraDbContext` sin entidades de negocio prematuras, una migración técnica inicial y verificaciones de conectividad/migración que no comprometan secretos ni acoplen la API a migraciones automáticas en producción.

## Alcance incluido

### 1. Dependencias y ubicación

- Usar Entity Framework Core y proveedor SQL Server estables compatibles con `net10.0`.
- Fijar versiones exactas, sin rangos flotantes ni paquetes prerelease.
- Colocar persistencia, configuraciones y migraciones en `AURA.Infrastructure`.
- Agregar sólo las dependencias necesarias para tooling EF y pruebas.
- Mantener la dirección de referencias ya verificada.

### 2. DbContext mínimo

Crear `AuraDbContext` en una carpeta coherente, por ejemplo `Persistence/`:

- constructor por `DbContextOptions<AuraDbContext>`;
- sin entidades de usuarios, tenants, proyectos o EXEC;
- sin tablas artificiales creadas sólo para “probar” EF;
- convenciones preparadas para timestamps UTC y configuraciones por entidad futuras, pero sin sobreingeniería;
- migraciones alojadas en `AURA.Infrastructure`.

La primera migración puede no crear tablas de dominio. Debe ser una migración técnica válida que permita a EF crear y registrar `__EFMigrationsHistory`, demostrando que el pipeline funciona sin introducir modelos prematuros.

### 3. Configuración segura

- Usar la clave lógica `ConnectionStrings:DefaultConnection` en runtime.
- Agregar `UserSecretsId` al proyecto de inicio apropiado para desarrollo.
- Permitir que tooling/design-time lea `AURA_DESIGNTIME_CONNSTR` o la configuración segura equivalente.
- La factory design-time debe fallar con un mensaje claro si no recibe conexión; nunca debe contener un fallback con credenciales.
- No versionar una connection string real ni una contraseña.
- No mostrar la connection string completa en logs o errores.

Documentar comandos con `<CONNECTION_STRING_DE_DESARROLLO>` como placeholder. Para la prueba local, tomá el valor real del documento rector y cargalo sin reproducirlo en tu devolución.

### 4. Registro de infraestructura

- Crear una extensión de DI en `AURA.Infrastructure`, por ejemplo `AddInfrastructure`, para registrar `AuraDbContext` y servicios técnicos.
- `AURA.Api` debe consumir esa extensión, sin configurar EF directamente en controladores.
- No ejecutar `Database.Migrate`, `EnsureCreated` ni seed automáticamente al iniciar la API.
- Si falta la conexión, la API debe conservar un liveness útil y expresar que no está lista mediante readiness, sin filtrar secretos.

### 5. Health checks

Conservar compatible `GET /health` como liveness de la API.

Agregar:

- `GET /health/live`: proceso activo, sin dependencia de SQL Server;
- `GET /health/ready`: incluye conectividad SQL Server cuando la conexión está configurada.

Comportamiento esperado:

- `/health` y `/health/live`: HTTP 200 si el proceso está sano;
- `/health/ready`: HTTP 200 cuando SQL Server es accesible y la migración está aplicada;
- `/health/ready`: HTTP 503 con respuesta segura cuando falta configuración o SQL Server no está accesible;
- ningún endpoint revela servidor, usuario, password ni connection string.

Usar `IHealthCheck` propio con `AuraDbContext.Database.CanConnectAsync` si evita una dependencia adicional. Mantener respuestas JSON pequeñas y consistentes.

### 6. Migración y scripts

Crear la migración `InitialInfrastructure` mediante tooling EF, no escribiéndola manualmente salvo corrección justificada del scaffolding.

Agregar scripts PowerShell seguros:

- `scripts/database-update.ps1`: aplica migraciones usando configuración segura y falla claramente si falta;
- `scripts/database-verify.ps1`: comprueba conexión, migraciones pendientes y readiness;
- ninguno debe imprimir el secreto;
- ninguno debe borrar, recrear o resetear bases;
- ninguno debe aceptar silenciosamente una base distinta de `AURA` sin advertirlo.

No incorporar la actualización de base dentro de `scripts/verify.ps1`, porque ese verificador debe seguir funcionando sin secretos. Podés agregar un mensaje/documentación que indique la verificación separada de base.

### 7. Pruebas automatizadas

Mantener las 8 pruebas actuales.

Agregar como mínimo:

- test de liveness sin connection string;
- test de readiness sin configuración que espere 503 y respuesta segura;
- test unitario de la validación/configuración de persistencia si se introduce lógica propia;
- test que pruebe que ningún arranque normal ejecuta migraciones automáticamente, mediante diseño verificable o prueba adecuada.

La prueba real contra SQL Server se ejecuta mediante el script separado y debe informarse como evidencia de integración local. No reemplazarla por EF InMemory para declarar compatibilidad SQL Server.

### 8. Documentación

Actualizar `README.md` raíz con:

- configuración de User Secrets con placeholder;
- variable design-time;
- creación/aplicación de migraciones;
- diferencia entre liveness y readiness;
- ejecución de `database-update.ps1` y `database-verify.ps1`;
- aclaración de que no hay migración automática en startup;
- resolución de errores sin revelar secretos.

## Fuera de alcance

- `Users`, `Tenants`, membresías, roles o permisos;
- autenticación, cookies, JWT, email o recuperación de contraseña;
- proyectos, EXEC, prompts o evidencia;
- seed de usuarios o datos demo;
- auditoría funcional u outbox;
- publicación en Staging/IIS;
- cambios visuales del frontend;
- conectores cloud o agente local;
- modificación del porcentaje o estado oficial del proyecto.

## Requisitos de seguridad

- No copiar la connection string real a ningún archivo versionado.
- No devolver la connection string en el informe.
- No registrar opciones completas de EF ni datos sensibles.
- No usar `EnsureCreated` como sustituto de migraciones.
- No borrar, recrear ni vaciar la base.
- No ejecutar migraciones automáticamente al iniciar la API.
- Verificar que el nombre de base efectivo sea `AURA` antes de aplicar cambios.
- No usar una cuenta productiva; la credencial del documento es sólo de desarrollo.

## Criterios de aceptación

1. Restore, build y tests existentes/nuevos finalizan correctamente.
2. `AuraDbContext` está en Infrastructure y la API lo registra mediante una extensión de DI.
3. Existe una migración EF `InitialInfrastructure` reproducible y sin tablas de negocio prematuras.
4. `database-update.ps1` aplica la migración sobre la base de desarrollo `AURA` sin imprimir secretos.
5. `database-verify.ps1` confirma conexión y cero migraciones pendientes.
6. `/health` y `/health/live` devuelven HTTP 200 sin depender de SQL Server.
7. `/health/ready` devuelve 503 seguro sin configuración y 200 con SQL Server configurado y migrado.
8. La API no migra ni crea la base automáticamente al arrancar.
9. Ningún archivo nuevo contiene la connection string o credenciales reales.
10. README y scripts permiten repetir el proceso sin contexto externo.
11. `Manual Aura`, `Metodologia ORCHESTRA` y la documentación original permanecen sin cambios.

## Verificación obligatoria

Ejecutar y registrar:

```powershell
.\scripts\verify.ps1
.\scripts\database-update.ps1
.\scripts\database-verify.ps1
```

Además:

1. iniciar la API sin connection string y verificar 200 en liveness y 503 en readiness;
2. iniciar la API con configuración segura y verificar 200 en readiness;
3. comprobar migraciones aplicadas y pendientes con tooling EF;
4. buscar secretos en archivos versionables;
5. detener sólo los procesos iniciados para estas pruebas.

Si la conexión documentada falla, no modifiques seguridad de SQL Server ni cambies credenciales. Informá el error sanitizado como bloqueo y dejá aprobadas sólo las verificaciones que sí pasaron.

## Formato obligatorio de devolución

Responder exactamente con:

1. **Estado:** completado, parcial o bloqueado.
2. **Resumen funcional.**
3. **Archivos creados/modificados.**
4. **Paquetes y versiones incorporadas.**
5. **Modelo de persistencia y ubicación de migraciones.**
6. **Configuración segura utilizada:** sin reproducir valores secretos.
7. **Migración:** creada, aplicada y pendientes.
8. **Health checks:** matriz endpoint/configuración/status observado.
9. **Pruebas ejecutadas:** comando, resultado y cantidad.
10. **Criterios de aceptación:** lista 1 a 11, cumplido o pendiente.
11. **Seguridad:** resultado de búsqueda de secretos y confirmación de no auto-migración.
12. **Decisiones y supuestos.**
13. **Riesgos, deuda o bloqueos.**
14. **Siguiente acción recomendada.**

No declares completada la intervención si no se aplicó y verificó la migración contra SQL Server real. No avances a modelos SaaS ni a publicación de Staging.
