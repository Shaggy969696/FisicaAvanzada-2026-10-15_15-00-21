# Reglas de Proyecto: Simulador Grúa Batería de Gravedad

## 1. Contexto y Seguimiento Obligatorio
- **Lectura inicial:** En cada nueva conversación, consultar primero `CONTEXTO_PROYECTO.md` para recuperar las decisiones de diseño, los requisitos de la cátedra y las tareas completadas.
- **Actualización obligatoria:** Tras completar cada solicitud, prompt o cambio técnico, actualizar `CONTEXTO_PROYECTO.md` manteniendo el registro exacto de lo realizado y el estado de las fases.

## 2. Restricciones Técnicas
- **Escala ArticulationBody:** Todos los GameObjects con `ArticulationBody` deben conservar escala local exacta `(1, 1, 1)`. La geometría visual escalada debe ubicarse en sub-objetos hijos sin collider.
- **Geometría:** Utilizar figuras geométricas simples (cubos, cilindros, esferas).
- **Publicación:** Builds WebGL compatibles con itch.io (sin Butler CLI, compresión deshabilitada para evitar problemas de servidor).
- **Git:** Mantener el repositorio limpio excluyendo `Library/`, `Temp/`, `Logs/` y `Build/`.
