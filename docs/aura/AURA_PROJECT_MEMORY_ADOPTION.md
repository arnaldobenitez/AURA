# AURA Project Memory — adopción rápida

Guía práctica para adoptar Project Memory en un proyecto local. Project Memory funciona sin red, base de datos ni
Git obligatorio.

## 1. Ejecutar el CLI local

Desde cualquier ubicación, usar el launcher del repositorio:

```powershell
.\tools\aura.ps1 project validate C:\ruta\al\proyecto
.\tools\aura.ps1 agent bootstrap C:\ruta\al\proyecto --json
.\tools\aura.ps1 manager inspect C:\ruta\al\proyecto --json
```

El launcher detecta la raíz del repositorio a partir de su propia ubicación. Si existe un CLI compilado, ejecuta
`aura.dll` directamente; de lo contrario usa `dotnet run`. No modifica PATH, no requiere permisos de administrador,
no instala herramientas globales y no usa red. Para una carpeta recién clonada, compilar primero:

```powershell
dotnet build AURA.sln
```

Si PowerShell bloquea la ejecución de scripts, usar la política permitida por el entorno o invocar el CLI equivalente:

```powershell
dotnet run --project src\AURA.ProjectMemory.Cli\AURA.ProjectMemory.Cli.csproj -- <comandos>
```

## 2. SDK requerido

`global.json` fija el SDK `10.0.201`. No debe modificarse solamente para pasar una prueba local.
Si `dotnet` informa que ese SDK no está instalado, instalarlo según la política del entorno o ejecutar desde una
máquina que lo tenga disponible. En desarrollo local también puede usarse un SDK compatible ya instalado cuando el
proyecto lo permita; en este repositorio se validó temporalmente con `10.0.400`, restaurando después `global.json`.

## 3. Inicializar y validar

```powershell
.\tools\aura.ps1 project init C:\ruta\al\proyecto --name "Mi proyecto"
.\tools\aura.ps1 project validate C:\ruta\al\proyecto
.\tools\aura.ps1 agent bootstrap C:\ruta\al\proyecto --json
```

La inicialización crea el Core mínimo:

```text
AURA.md
.aura/project.json
docs/00-PROYECTO.md
docs/01-ESTADO-ACTUAL.md
docs/historial/
```

Si existen archivos Core parciales pero falta `.aura/project.json`, `project init` falla con un diagnóstico de
inicialización parcial y no sobrescribe nada. La reparación es manual y deliberada; no se inventa `projectId`.

## 4. Trabajar con sesiones

Para una tarea relevante:

```powershell
.\tools\aura.ps1 project session start --path C:\ruta\al\proyecto --objective "Objetivo" --role CODER --json
.\tools\aura.ps1 project session status --path C:\ruta\al\proyecto --json
.\tools\aura.ps1 project session close --path C:\ruta\al\proyecto --session <uuid> --input <payload.json> --json
```

El cierre genera un registro semántico append-only en `docs/historial/`. No se guardan transcripts ni secretos.

## 5. Project Manager

```powershell
.\tools\aura.ps1 manager inspect C:\ruta\al\proyecto --json
.\tools\aura.ps1 manager remember C:\ruta\al\proyecto --alias mi-proyecto --json
.\tools\aura.ps1 manager list-known --json
.\tools\aura.ps1 manager history C:\ruta\al\proyecto --take 5 --json
.\tools\aura.ps1 manager handoff C:\ruta\al\proyecto --json
.\tools\aura.ps1 manager forget mi-proyecto --json
```

El índice local se guarda en `~/.aura/projects/index.json`. Solo contiene identidad portable, alias, raíz local y
marcas de tiempo. No forma parte del proyecto y no guarda secretos, tokens, contenido documental ni URLs remotas.

Para pruebas aisladas se puede usar `--index-path <archivo>`.

## 6. Git y continuidad entre agentes

Git es opcional. Un árbol `dirty` no invalida Project Memory; debe interpretarse como contexto de trabajo y revisarse
antes de entregar cambios. Los conflictos Git en documentos Core sí requieren resolución manual.

El siguiente agente debe:

1. ejecutar `agent bootstrap`;
2. leer `AURA.md`, `.aura/project.json`, `docs/00-PROYECTO.md` y `docs/01-ESTADO-ACTUAL.md`;
3. consultar `manager handoff` si necesita una síntesis rápida;
4. abrir una sesión antes del primer cambio persistente relevante;
5. cerrar la sesión y declarar los documentos actualizados.

## 7. Smoke test sin red

Desde la raíz del repositorio:

```powershell
.\tools\smoke-project-memory.ps1
```

El script usa una carpeta temporal y un índice temporal, ejecuta init, validate, bootstrap, ciclo de sesión,
Project Manager, historial y handoff, y elimina únicamente los artefactos temporales que creó.
