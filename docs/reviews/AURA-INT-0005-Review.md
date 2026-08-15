# Revisión AURA-INT-0005

## Resultado

**AURA-INT-0004 aceptada. ETAPA 1 continúa en curso.**

La persistencia SQL Server y la migración técnica inicial fueron contrastadas con el repositorio y con verificaciones independientes. Los 11 criterios de aceptación se consideran cumplidos.

## Evidencia verificada

- EF Core SQL Server `10.0.11` y `dotnet-ef 10.0.11`, con versiones fijas.
- `AuraDbContext`, factory design-time, readiness y migraciones alojados en `AURA.Infrastructure`.
- `InitialInfrastructure` no crea tablas de negocio.
- No existen `Database.Migrate`, `EnsureCreated` ni seed en el arranque normal.
- `AddInfrastructure` mantiene la configuración de persistencia fuera de `AURA.Api`.
- `scripts/verify.ps1`: build de 6 proyectos sin errores ni advertencias.
- Tests: 9 unitarios + 5 de integración, 14/14 aprobados.
- Frontend: instalación reproducible, typecheck, lint y build aprobados; 0 vulnerabilidades informadas.
- `database-verify.ps1`: conexión correcta y cero migraciones pendientes.
- API viva con configuración normal: `/health`, `/health/live` y `/health/ready` respondieron HTTP 200; readiness consultó `__EFMigrationsHistory`.
- No se encontraron credenciales reales en archivos versionables.

## Observaciones

### Readiness en entorno aislado

Dentro del aislamiento de revisión, una escritura denegada al Windows Event Log convirtió un health check fallido en una excepción de logging. Fuera del aislamiento, con el entorno normal y SQL Server accesible, readiness respondió correctamente. No bloquea esta intervención, pero el smoke de Staging deberá ejecutarse con la identidad real del proceso para confirmar permisos de logging.

### Verificador de base

`database-verify.ps1` comprueba conexión y migraciones pendientes. No invoca por sí mismo el endpoint de readiness, aunque ese comportamiento está cubierto por tests y fue verificado en vivo. Se acepta como desviación menor; el próximo smoke de Staging comprobará los endpoints explícitamente.

### Configuración de instancia

El informe de VS Code indica una instancia de desarrollo distinta de `localhost\SQLEXPRESS`, que figura en el documento original. La configuración activa funciona y apunta a una base separada llamada `AURA`, pero el endpoint exacto debe permanecer como configuración operativa segura. Antes de producción se debe confirmar formalmente aislamiento, propiedad, backups y credenciales de mínimo privilegio.

## Decisión de gate

AURA-INT-0004 queda aceptada. **ETAPA 1 aún no se cierra** porque falta producir un paquete de Staging reproducible y ejecutar su smoke test con configuración equivalente a producción.

## Siguiente intervención

Ejecutar `AURA-INT-0006-Staging-Publish-Smoke.md`. Si su artefacto, despliegue temporal y smoke test cumplen, se podrá decidir el cierre de ETAPA 1 y elevar la evolución global a 25%.
