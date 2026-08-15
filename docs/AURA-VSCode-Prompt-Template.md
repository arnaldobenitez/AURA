# AURA - Plantilla de prompt para VS Code

Esta plantilla será preparada por ChatGPT como analista funcional y entregada manualmente al entorno VS Code encargado de la codificación.

## Encabezado

- **ID de intervención:** `AURA-INT-XXXX`
- **Etapa:** `ETAPA N - Nombre`
- **Título:** resultado concreto esperado
- **Fecha del prompt:** `YYYY-MM-DD HH:mm:ss -03:00`
- **Estado inicial:** listo para codificación

## Rol del entorno de codificación

Actuá como desarrollador responsable de implementar exclusivamente el alcance indicado. Antes de modificar archivos, inspeccioná el repositorio y respetá su arquitectura, convenciones y cambios existentes. No amplíes el alcance sin informarlo.

## Contexto funcional

Describir brevemente:

- problema que se resuelve;
- usuario afectado;
- comportamiento actual;
- comportamiento esperado;
- documentos que son fuente de verdad.

## Objetivo

Definir un único resultado verificable para la intervención.

## Alcance incluido

1. Requisito o cambio incluido.
2. Requisito o cambio incluido.
3. Documentación mínima que debe quedar actualizada.

## Fuera de alcance

- funcionalidades que no deben implementarse;
- refactors no necesarios;
- integraciones reservadas para etapas posteriores.

## Requisitos funcionales

1. Comportamiento observable.
2. Regla de negocio.
3. Permisos y mensajes esperados.

## Requisitos técnicos

- proyectos y capas involucrados;
- contratos/API/datos afectados;
- seguridad y aislamiento multi-tenant;
- compatibilidad y migraciones;
- logging, auditoría u observabilidad necesaria.

## Criterios de aceptación

1. Dado un contexto, cuando ocurre una acción, entonces se observa el resultado esperado.
2. Caso negativo o de autorización.
3. Persistencia, auditoría o respuesta esperada.
4. Build y pruebas requeridas aprobadas.

## Verificación obligatoria

- compilar los proyectos afectados;
- ejecutar pruebas existentes relevantes;
- crear pruebas nuevas para reglas o regresiones;
- comprobar migraciones cuando correspondan;
- informar cualquier prueba no ejecutada y el motivo.

## Restricciones

- no incorporar secretos al repositorio;
- no modificar archivos ajenos al alcance;
- preservar cambios existentes del usuario;
- no aplicar literalmente SCHEMA al proceso interno;
- priorizar la porción vertical mínima que acerque a producción.

## Formato de devolución obligatorio

Responder con:

1. **Estado:** completado, parcial o bloqueado.
2. **Resumen funcional.**
3. **Archivos creados/modificados.**
4. **Base de datos/configuración:** migraciones y variables.
5. **Pruebas ejecutadas:** comando y resultado.
6. **Criterios de aceptación:** cumplidos o pendientes.
7. **Decisiones/supuestos.**
8. **Riesgos o deuda.**
9. **Siguiente acción recomendada.**

No declarar la intervención completada si el build o una prueba obligatoria falla, salvo que el bloqueo quede explicado con evidencia.
