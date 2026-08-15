# Prompt para VS Code - AURA-INT-0006

## Encabezado

- **ID de intervención:** `AURA-INT-0006`
- **Etapa:** `ETAPA 1 - Base desplegable`
- **Título:** generar paquete reproducible de Staging y ejecutar smoke test
- **Fecha del prompt:** `2026-08-12 01:04:11 -03:00`
- **Estado inicial:** listo para codificación

## Rol del entorno de codificación

Actuá como desarrollador/operador senior. Debés convertir la base ya verificada de AURA en artefactos reproducibles de Staging y demostrar su funcionamiento con un despliegue temporal equivalente a producción. No avances a autenticación, tenants ni funcionalidades de negocio.

Trabajá en:

```text
D:\Desarrollos\AURA
```

## Fuentes de verdad

Leé en orden:

1. `docs/AURA-Development-Plan.md`
2. `docs/AURA-Project-Status.md`
3. `docs/reviews/AURA-INT-0005-Review.md`
4. `README.md`
5. `src/AURA.Web/public/web.config`

No modifiques `docs/Proyecto AURA.docx`, `Manual Aura` ni `Metodologia ORCHESTRA`.

## Contexto funcional

AURA ya cuenta con solución .NET 10, frontend React/Vite, SQL Server, migración técnica, liveness y readiness. ETAPA 1 sólo puede cerrarse cuando exista un proceso repetible para producir artefactos de Staging y un smoke test que valide API, frontend y base bajo configuración no Development.

No existe todavía un servidor remoto de Staging documentado. Esta intervención debe implementar y verificar un **Staging local reproducible**, preparado para trasladarse a IIS sin cambiar el código. No inventes hostname, credenciales, certificados ni rutas remotas. Si durante la intervención el usuario te proporciona un IIS real, podés desplegar allí sólo con autorización explícita y documentar el destino.

## Objetivo

Generar paquetes Release separados para API y frontend, iniciarlos temporalmente con `ASPNETCORE_ENVIRONMENT=Staging`, ejecutar un smoke test automatizado y producir un runbook suficiente para repetir el despliegue o trasladarlo a IIS.

## Alcance incluido

### 1. Configuración Staging segura

- Crear `appsettings.Staging.json` sólo con valores no sensibles.
- Mantener `ConnectionStrings:DefaultConnection` fuera de archivos versionados.
- Habilitar CORS en cualquier entorno únicamente cuando haya una lista explícita de orígenes permitidos; sin `AllowAnyOrigin`.
- Configurar logging para consola de forma segura y evitar que un fallo de Windows Event Log derribe requests bajo identidades restringidas.
- Mantener OpenAPI deshabilitado fuera de Development, salvo decisión explícita y documentada de exponer sólo el documento en Staging.
- No ejecutar migraciones automáticamente en startup.

### 2. Artefactos reproducibles

Crear `scripts/publish-staging.ps1` que:

- ejecute primero `scripts/verify.ps1` o falle si la verificación no pasa;
- publique `AURA.Api` en Release dentro de `artifacts/staging/api`;
- ejecute `npm ci` y build de `AURA.Web` con una URL API parametrizable;
- copie el frontend a `artifacts/staging/web` incluyendo `web.config`;
- limpie sólo sus directorios de artefactos previamente validados;
- nunca copie User Secrets, `.env.local`, logs, símbolos innecesarios ni credenciales;
- genere un `manifest.json` con versión, timestamp UTC, SDK/Node, archivos principales y hashes SHA-256;
- sea repetible desde un checkout limpio.

`artifacts/` debe continuar ignorado por Git.

### 3. Ejecución temporal de Staging

Crear `scripts/start-staging.ps1` que:

- requiera configuración segura de SQL Server mediante variable de entorno o User Secrets, sin imprimirla;
- ejecute la API publicada con `ASPNETCORE_ENVIRONMENT=Staging` en un puerto parametrizable, default `5236`;
- sirva el frontend compilado en un puerto parametrizable, default `4173`, usando una dependencia local/versionada o mecanismo reproducible;
- configure el origen permitido exacto del frontend;
- use procesos ocultos o claramente identificables y guarde sus PID en un archivo dentro de `artifacts/staging/runtime`;
- no mate procesos ajenos ni ocupe puertos silenciosamente;
- falle si un puerto ya está ocupado;
- no muestre secretos.

Crear `scripts/stop-staging.ps1` que detenga exclusivamente los PID registrados y valide que corresponden a los procesos iniciados por AURA.

### 4. Smoke test

Crear `scripts/smoke-staging.ps1` que valide como mínimo:

1. frontend HTTP 200 y contenido AURA;
2. `/health` HTTP 200;
3. `/health/live` HTTP 200;
4. `/health/ready` HTTP 200 contra SQL Server migrado;
5. header `X-Correlation-Id` presente;
6. respuesta de readiness sin secretos;
7. OpenAPI no expuesto en Staging;
8. una ruta SPA desconocida devuelve `index.html` cuando el servidor elegido soporta fallback;
9. artefacto/manifest presentes y hashes verificables;
10. logs sin connection string ni password.

Debe devolver código distinto de cero ante cualquier fallo y mostrar un resumen seguro.

### 5. IIS y portabilidad

- Revisar `src/AURA.Web/public/web.config` y corregirlo sólo si es necesario para un sitio frontend separado.
- Generar un `web.config` de API mediante `dotnet publish`, sin secretos embebidos.
- Documentar el modelo recomendado de IIS: aplicación API separada y sitio/aplicación SPA separada, HTTPS, app pools y variables externas.
- No ejecutar `iisreset`, crear sitios, tocar certificados ni escribir en `C:\inetpub` en esta intervención salvo autorización explícita.

### 6. Runbook

Crear `docs/AURA-Staging-Runbook.md` con:

- prerrequisitos;
- variables requeridas con placeholders;
- publish, start, smoke y stop;
- aplicación manual de migraciones antes del arranque;
- estructura de artefactos;
- traslado a IIS;
- permisos mínimos de filesystem y logging;
- rollback al artefacto anterior;
- diagnóstico de readiness, CORS y SPA fallback;
- confirmación de que secretos no viven en archivos publicados.

Actualizar el `README.md` raíz con un enlace breve al runbook y comandos principales.

### 7. Pruebas automatizadas

- Mantener las 14 pruebas existentes.
- Agregar tests para la nueva política CORS/configuración si contiene lógica propia.
- Agregar una prueba que confirme que Event Log no es requisito para responder health checks.
- Los scripts deben poder probarse de forma segura o estar estructurados para que su validación no mate procesos ajenos.

## Fuera de alcance

- despliegue productivo real;
- creación o modificación de infraestructura remota sin autorización;
- autenticación, Platform Owner, tenants, usuarios o permisos;
- modelos de proyecto o EXEC;
- dominio, DNS, TLS/certificados o secretos definitivos;
- Docker/Kubernetes;
- CI/CD remoto;
- agente local o conectores cloud;
- cambios visuales no necesarios;
- actualización del porcentaje oficial: la realizará ChatGPT tras la revisión.

## Requisitos de seguridad

- No versionar ni copiar connection strings o passwords a artefactos.
- No imprimir secretos en consola, manifest o logs.
- No usar CORS abierto.
- No auto-migrar al iniciar.
- No borrar bases ni artefactos fuera de `artifacts/staging`.
- No detener procesos que no estén registrados y validados.
- No ejecutar IIS como administrador ni cambiar el sistema sin autorización.
- Los artefactos deben ejecutarse con identidad de mínimo privilegio.

## Criterios de aceptación

1. `scripts/verify.ps1` continúa pasando con todas las pruebas.
2. `publish-staging.ps1` crea API y web Release más manifest con hashes.
3. Ningún artefacto contiene secretos ni archivos locales sensibles.
4. La API publicada corre con entorno `Staging` sin auto-migración.
5. El frontend publicado responde y consulta correctamente la API de Staging.
6. Smoke test aprueba los 10 controles indicados.
7. Readiness devuelve 200 contra la base AURA migrada.
8. OpenAPI permanece deshabilitado en Staging.
9. Start/stop sólo administran procesos propios y fallan de forma segura.
10. CORS acepta únicamente el origen configurado.
11. El logging no depende de permisos de Windows Event Log para responder.
12. Runbook permite repetir el proceso y trasladarlo a IIS sin contexto externo.
13. Build y tests no modifican `Manual Aura`, `Metodologia ORCHESTRA` ni documentación original.

## Verificación obligatoria

Ejecutar y registrar, sin revelar secretos:

```powershell
.\scripts\verify.ps1
.\scripts\database-verify.ps1
.\scripts\publish-staging.ps1
.\scripts\start-staging.ps1
.\scripts\smoke-staging.ps1
.\scripts\stop-staging.ps1
```

Después del stop:

- confirmar que no quedan listeners en los puertos usados;
- validar el manifest y hashes;
- buscar secretos en archivos versionables y en `artifacts/staging`;
- informar tamaños de artefactos y cantidad final de pruebas.

Si no es posible servir la SPA con fallback usando el mecanismo local elegido, no ocultes el incumplimiento. Documentá el bloqueo o implementá un servidor reproducible de mínimo alcance; no uses un servidor instalado globalmente que otro desarrollador no tenga.

## Formato obligatorio de devolución

Responder exactamente con:

1. **Estado:** completado, parcial o bloqueado.
2. **Resumen funcional.**
3. **Archivos creados/modificados.**
4. **Cambios de configuración Staging y CORS.**
5. **Artefactos:** rutas, tamaños y manifest.
6. **Ejecución temporal:** puertos, entorno y procesos, sin secretos.
7. **Smoke test:** controles 1 a 10 y resultado observado.
8. **Pruebas:** comandos, cantidades y resultados.
9. **Seguridad:** búsqueda de secretos, logging y procesos.
10. **IIS/runbook:** modelo documentado y pendientes reales.
11. **Criterios de aceptación:** lista 1 a 13, cumplido o pendiente.
12. **Decisiones y supuestos.**
13. **Riesgos, deuda o bloqueos.**
14. **Siguiente acción recomendada.**

No declares completada la intervención si falla el smoke test, readiness, el frontend publicado o la verificación de secretos. No avances a ETAPA 2.
