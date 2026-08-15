# AURA.Web

Frontend mínimo de AURA (React + TypeScript + Vite). Ver el `README.md` en la raíz del repositorio para prerrequisitos, comandos y contexto del proyecto.

## Scripts

- `npm run dev`: servidor de desarrollo con recarga en caliente.
- `npm run build`: `tsc -b` seguido de `vite build`.
- `npm run typecheck`: sólo verificación de tipos (`tsc -b`).
- `npm run lint`: `oxlint`.
- `npm run preview`: sirve el build de producción localmente.

## Configuración

Copiar `.env.example` a `.env.local` y ajustar `VITE_API_BASE_URL` si la API no corre en `http://localhost:5235`. En desarrollo, `/health` también se proxea automáticamente hacia la API (ver `vite.config.ts`), por lo que `VITE_API_BASE_URL` puede dejarse vacío.
