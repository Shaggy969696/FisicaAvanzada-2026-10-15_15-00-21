# Documento de Contexto y Seguimiento del Proyecto
**Proyecto:** Simulador de Grúa de Batería de Gravedad (`ArticulationBody`)  
**Materia:** Simuladores · Licenciatura en Desarrollo de Videojuegos y Simulaciones (2.º año - UAI)  
**Motor:** Unity 6 (`6000.4.5f1` / 6.3 LTS) · URP · Nuevo Input System  
**Repositorio:** `https://github.com/Shaggy969696/FisicaAvanzada-2026-10-15_15-00-21.git`

---

## 1. Directrices y Decisiones de Diseño Confirmadas
1. **Mecanismo elegido:** **Grúa de Batería de Gravedad** (según especificación en `Estructura de bateria de gravedad.md`).
2. **Operación del simulador:** Almacenamiento y descarga de energía potencial gravitatoria mediante el izado, traslado radial/angular y apilado en altura de bloques de hormigón (`Rigidbody` convencionales), sujetados exclusivamente por fuerza normal y fricción seca (`PhysicsMaterial` con `FrictionCombine.Maximum`), sin usar *parenting* ni joints dinámicos por código.
3. **Geometría y Modelado:** Uso exclusivo de **figuras geométricas simples** (primitivas de Unity: cubos, cilindros, esferas). No se requiere detalle artístico complejo ni ProBuilder.
4. **Regla estricta de escala:** Todos los GameObjects que posean un componente `ArticulationBody` mantienen escala local exacta `(1, 1, 1)`. Las dimensiones visuales se aplican en sub-objetos hijos (`Visual_*`) sin `Collider`, y los `Collider` se dimensionan por propiedades (`size` / `radius`) en el nodo físico.
5. **Despliegue en itch.io:** Se realizará de forma manual con archivo `.zip` de la build WebGL (**no** se implementará Butler CLI).
6. **Documentación técnica:** Se implementan **Diagramas Mermaid** para representar el árbol cinemático, tipos de joint, ejes y límites.

---

## 2. Verificación de Requisitos de la Cátedra

| Requisito de Cátedra | Mínimo Exigido | Nuestro Diseño (Batería de Gravedad) | Estado |
| :--- | :--- | :--- | :--- |
| **Árbol de Articulación** | Al menos 1 con raíz `immovable` | 1 árbol con raíz `BaseTorre` (`immovable = true`) | Cumple |
| **Cantidad de Cuerpos** | Entre 6 y 10 `ArticulationBody` | **8 cuerpos** (`BaseTorre`, `BrazoPluma`, `CarroRiel`, `EjeCabestrante`, `CabezalPinza`, `ChasisGarra`, `DedoGarra_Izq`, `DedoGarra_Der`) | Cumple |
| **Tipos de Joint** | Al menos 3 distintos | **4 tipos** (`RevoluteJoint`, `PrismaticJoint`, `SphericalJoint`, `FixedJoint`) | Supera |
| **Joints con Drive** | Al menos 4 controlados | **5 joints con `xDrive`** (`BrazoPluma`, `CarroRiel`, `EjeCabestrante`, `DedoGarra_Izq`, `DedoGarra_Der`) | Supera |
| **Joints Pasivos** | Al menos 1 por gravedad/contacto | **1 joint pasivo** (`CabezalPinza` con `SphericalJoint` sin stiffness, oscilación pendular) | Cumple |
| **Entrada (Input)** | UI o Input System | **4 Sliders + 3 Botones en UI** y soporte simultáneo de **Teclado (Input System)** | Implementado |
| **Lectura de Estado** | Target vs. Valor Real en pantalla | Panel de telemetría en vivo por cada joint controlado y del joint pasivo | Implementado |
| **Interacción Física** | Tarea con `Rigidbody` comunes | 3 Bloques de carga prismáticos manipulados por fricción pura | Implementado |

---

## 3. Seguimiento de Fases y Tareas

### Fase 1: Diagrama y Repositorio (Día 1) — COMPLETADA
- [x] Inicialización del repositorio Git con `.gitignore` oficial de Unity (excluyendo `Library`, `Temp`, `Logs`, `Build`).
- [x] Creación del documento de contexto y seguimiento (`CONTEXTO_PROYECTO.md`).
- [x] Definición completa del árbol cinemático (8 cuerpos, tipos de joint, ejes, límites, masas y drives).
- [x] Elaboración de la documentación con **Diagrama Mermaid** en `README.md`.

### Fase 2: Ensamblado Físico "En Caída Libre" — COMPLETADA Y VERIFICADA
- [x] Creación del generador procedural de editor (`Assets/Editor/GravityBatteryBuilder.cs`) con figuras geométricas simples y escala `(1, 1, 1)`.
- [x] Corrección de enumeradores de la API de Unity 6 (`ArticulationJointType.*Joint` y `ArticulationDofLock.*Motion`).
- [x] Prueba en Unity Editor (`1. Construir en Modo Caída Libre`): verificado que la pinza cae por gravedad hasta el límite sin saltar y se mueve libremente sin drives. Se deshabilitó la interactividad de Sliders/Botones cuando `drivesActivos == false` para mayor claridad visual.

### Fase 3: Sintonización de Drives — COMPLETADA Y VERIFICADA
- [x] Calibración escalonada de `stiffness`, `damping` y `forceLimit` de la raíz a la punta (`80000` en base hasta `25000` en dedos, con `forceLimit = 12000` en mordazas).
- [x] Creación automática del material físico `HighFrictionGrip.physicMaterial` (`staticFriction = 2.5`, `dynamicFriction = 2.0`, `FrictionCombine = Maximum`).

### Fase 4: Controlador y UI de Telemetría — COMPLETADA Y VERIFICADA
- [x] Implementación de `Assets/Scripts/Mechanics/GravityBatteryController.cs` (actualización de `xDrive.target` en `FixedUpdate`, lectura de `jointPosition` y entradas por UI + Teclado).
- [x] Implementación de extensión visual continua para `Visual_CableRigido` en `LateUpdate()`, conectando siempre el `CarroRiel` con el `CabezalPinza` sin perder la escala `(1, 1, 1)` del `ArticulationBody`.
- [x] Implementación de `Assets/Scripts/UI/GravityBatteryTelemetryUI.cs` y verificación de funcionamiento con Sliders y Botones en Unity.

### Fase 5: Prueba WebGL Temprana — LISTA PARA EJECUTAR
- [x] Automatización en menú `Simulador -> Batería de Gravedad -> 3. Configurar y Compilar Prueba WebGL (Fase 5)` (desactiva compresión conflictiva para itch.io y genera la carpeta `Build/WebGL`).
- [ ] Ejecución de la primera Build WebGL y prueba en navegador.
