using System;
using System.IO;
using FisicaAvanzada.Mechanics;
using FisicaAvanzada.UI;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FisicaAvanzada.Editor
{
    /// <summary>
    /// Generador procedural completo del Simulador de Grúa de Batería de Gravedad.
    /// Soporta Fase 2 (Caída Libre), Fase 3 (Drives Calibrados), Fase 4 (Controlador + UI HMI Distribuida) y Fase 5 (WebGL).
    /// </summary>
    public static class GravityBatteryBuilder
    {
        private const string MaterialsFolder = "Assets/Materials";
        private const string RootObjectName = "GruaBateriaGravedad";
        private const string EnvironmentObjectName = "Entorno_BateriaGravedad";
        private const string CanvasObjectName = "Canvas_TelemetriaBateria";

        // Paleta cromática sincronizada entre piezas 3D y controles UI (Color-Coding Industrial)
        private static readonly Color ColorPluma = new Color(0.92f, 0.72f, 0.12f, 1f);        // Amarillo Ámbar (Giro Revolute Y)
        private static readonly Color ColorCarro = new Color(0.92f, 0.44f, 0.12f, 1f);        // Naranja Intenso (Carro Prismatic Z)
        private static readonly Color ColorCabestrante = new Color(0.18f, 0.80f, 0.44f, 1f);  // Verde Esmeralda (Izado Prismatic -Y)
        private static readonly Color ColorGarra = new Color(0.88f, 0.24f, 0.20f, 1f);        // Rojo Coral (Mordazas Prismatic ±X)
        private static readonly Color ColorPasivo = new Color(0.15f, 0.72f, 0.92f, 1f);       // Cian Eléctrico (Rótula Spherical Pasiva)

        [MenuItem("Simulador/Batería de Gravedad/1. Construir en Modo Caída Libre (Fase 2 - Sin Drives)", false, 10)]
        public static void BuildFreeFallMode()
        {
            BuildCompleteScene(enableDrives: false);
            Debug.Log("[GravityBatteryBuilder] Escena construida en MODO CAÍDA LIBRE (Fase 2 - Sin Drives). Entra en Play para verificar la caída por gravedad y el respeto de los límites.");
        }

        [MenuItem("Simulador/Batería de Gravedad/2. Construir Simulador Completo (Fases 3 y 4 - Drives + UI)", false, 20)]
        public static void BuildDrivenMode()
        {
            BuildCompleteScene(enableDrives: true);
            Debug.Log("[GravityBatteryBuilder] Simulador completo construido (Fases 3 y 4: Drives calibrados + Controlador + HUD Industrial Distribuido).");
        }

        [MenuItem("Simulador/Batería de Gravedad/3. Configurar y Compilar Prueba WebGL (Fase 5)", false, 30)]
        public static void ConfigureAndBuildWebGL()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.isDirty)
            {
                EditorSceneManager.SaveScene(activeScene);
            }

            string scenePath = activeScene.path;
            if (string.IsNullOrEmpty(scenePath))
            {
                scenePath = "Assets/Scenes/SampleScene.unity";
                EditorSceneManager.SaveScene(activeScene, scenePath);
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(scenePath, true)
            };

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.runInBackground = true;

            string buildPath = Path.Combine(Directory.GetCurrentDirectory(), "Build", "WebGL");
            Directory.CreateDirectory(buildPath);

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = buildPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            Debug.Log($"[GravityBatteryBuilder] Iniciando compilación WebGL en: {buildPath} ...");
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[GravityBatteryBuilder] Build WebGL exitosa en '{buildPath}' ({report.summary.totalSize / (1024 * 1024)} MB). Lista para comprimir en .zip y subir a itch.io.");
                EditorUtility.RevealInFinder(buildPath);
            }
            else
            {
                Debug.LogError($"[GravityBatteryBuilder] La Build WebGL finalizó con estado: {report.summary.result}. Revisa si el módulo WebGL Build Support está instalado en Unity Hub.");
            }
        }

        public static void BuildCompleteScene(bool enableDrives)
        {
            EnsureMaterialsFolder();

            DestroyExistingIfPresent(RootObjectName);
            DestroyExistingIfPresent(EnvironmentObjectName);
            DestroyExistingIfPresent(CanvasObjectName);

            PhysicsMaterial highFrictionMat = GetOrCreateHighFrictionMaterial();
            Material towerMat = GetOrCreateColorMaterial("Mat_Torre", new Color(0.22f, 0.25f, 0.28f));
            Material boomMat = GetOrCreateColorMaterial("Mat_Pluma", ColorPluma);
            Material trolleyMat = GetOrCreateColorMaterial("Mat_Carro", ColorCarro);
            Material winchMat = GetOrCreateColorMaterial("Mat_Cabestrante", ColorCabestrante);
            Material passiveHeadMat = GetOrCreateColorMaterial("Mat_CabezalPasivo", ColorPasivo);
            Material frameMat = GetOrCreateColorMaterial("Mat_ChasisGarra", new Color(0.28f, 0.30f, 0.34f));
            Material fingerMat = GetOrCreateColorMaterial("Mat_DedoGarra", ColorGarra);
            Material blockMat = GetOrCreateColorMaterial("Mat_BloqueCarga", new Color(0.56f, 0.59f, 0.63f));
            Material groundMat = GetOrCreateColorMaterial("Mat_Suelo", new Color(0.15f, 0.17f, 0.20f));
            Material platformMat = GetOrCreateColorMaterial("Mat_Plataforma", new Color(0.20f, 0.44f, 0.32f));

            // 1. Construir Entorno y Bloques de Carga (Rigidbody comunes)
            BuildEnvironmentAndPayloads(groundMat, platformMat, blockMat, highFrictionMat, enableDrives);

            // 2. Construir el Árbol de ArticulationBody (8 cuerpos con escala (1,1,1)) + Controlador
            GravityBatteryController controller = BuildArticulationCrane(
                enableDrives,
                highFrictionMat,
                towerMat,
                boomMat,
                trolleyMat,
                winchMat,
                passiveHeadMat,
                frameMat,
                fingerMat);

            // 3. Construir la Interfaz Gráfica HMI Distribuida (Controles Espaciales + Telemetría Target vs Real)
            BuildTelemetryUI(controller, enableDrives);

            // 4. Posicionar y configurar cámara principal con seguimiento orbital sin oscilación pasiva
            SetupMainCamera(controller);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static GravityBatteryController BuildArticulationCrane(
            bool enableDrives,
            PhysicsMaterial highFrictionMat,
            Material towerMat,
            Material boomMat,
            Material trolleyMat,
            Material winchMat,
            Material passiveHeadMat,
            Material frameMat,
            Material fingerMat)
        {
            // =========================================================================
            // 1. BaseTorre (Root, Immovable = true)
            // =========================================================================
            GameObject baseTorre = CreateArticulationNode(RootObjectName, null, Vector3.zero);
            ArticulationBody abBase = baseTorre.GetComponent<ArticulationBody>();
            abBase.immovable = true;
            abBase.mass = 500f;
            abBase.useGravity = false;

            BoxCollider baseCol = baseTorre.AddComponent<BoxCollider>();
            baseCol.center = new Vector3(0f, 3.5f, 0f);
            baseCol.size = new Vector3(1.1f, 7.0f, 1.1f);

            AddVisualPrimitive(baseTorre.transform, "Visual_Columna", PrimitiveType.Cube,
                new Vector3(0f, 3.5f, 0f), new Vector3(1.0f, 7.0f, 1.0f), towerMat);
            AddVisualPrimitive(baseTorre.transform, "Visual_Pedestal", PrimitiveType.Cube,
                new Vector3(0f, 0.2f, 0f), new Vector3(2.4f, 0.4f, 2.4f), towerMat);

            // =========================================================================
            // 2. BrazoPluma (RevoluteJoint - Eje (0, 1, 0) Vertical)
            // =========================================================================
            GameObject brazoPluma = CreateArticulationNode("BrazoPluma", baseTorre.transform, new Vector3(0f, 7.15f, 0f));
            ArticulationBody abPluma = brazoPluma.GetComponent<ArticulationBody>();
            abPluma.jointType = ArticulationJointType.RevoluteJoint;
            abPluma.mass = 120f;
            abPluma.useGravity = true;
            abPluma.matchAnchors = true;
            abPluma.anchorPosition = Vector3.zero;
            abPluma.anchorRotation = Quaternion.Euler(0f, 0f, 90f);
            abPluma.twistLock = ArticulationDofLock.LimitedMotion;
            abPluma.xDrive = CreateDrive(
                lowerLimit: -180f,
                upperLimit: 180f,
                stiffness: enableDrives ? 80000f : 0f,
                damping: enableDrives ? 15000f : 50f,
                forceLimit: enableDrives ? 100000f : 0f,
                target: 0f);

            BoxCollider plumaCol = brazoPluma.AddComponent<BoxCollider>();
            plumaCol.center = new Vector3(0f, 0.25f, 2.2f);
            plumaCol.size = new Vector3(0.7f, 0.45f, 7.6f);

            AddVisualPrimitive(brazoPluma.transform, "Visual_VigaPluma", PrimitiveType.Cube,
                new Vector3(0f, 0.25f, 2.2f), new Vector3(0.65f, 0.45f, 7.6f), boomMat);
            AddVisualPrimitive(brazoPluma.transform, "Visual_Contrapeso", PrimitiveType.Cube,
                new Vector3(0f, 0.15f, -1.2f), new Vector3(1.1f, 0.8f, 1.2f), towerMat);

            // =========================================================================
            // 3. CarroRiel (PrismaticJoint - Eje (0, 0, 1) Radial sobre la pluma)
            // =========================================================================
            GameObject carroRiel = CreateArticulationNode("CarroRiel", brazoPluma.transform, new Vector3(0f, -0.15f, 1.5f));
            ArticulationBody abCarro = carroRiel.GetComponent<ArticulationBody>();
            abCarro.jointType = ArticulationJointType.PrismaticJoint;
            abCarro.mass = 40f;
            abCarro.useGravity = true;
            abCarro.matchAnchors = true;
            abCarro.anchorPosition = Vector3.zero;
            abCarro.anchorRotation = Quaternion.Euler(0f, -90f, 0f);
            abCarro.linearLockX = ArticulationDofLock.LimitedMotion;
            abCarro.linearLockY = ArticulationDofLock.LockedMotion;
            abCarro.linearLockZ = ArticulationDofLock.LockedMotion;
            abCarro.xDrive = CreateDrive(
                lowerLimit: 0.0f,
                upperLimit: 4.5f,
                stiffness: enableDrives ? 50000f : 0f,
                damping: enableDrives ? 8000f : 25f,
                forceLimit: enableDrives ? 60000f : 0f,
                target: enableDrives ? 2.0f : 0.0f);

            BoxCollider carroCol = carroRiel.AddComponent<BoxCollider>();
            carroCol.center = Vector3.zero;
            carroCol.size = new Vector3(0.85f, 0.28f, 0.85f);

            AddVisualPrimitive(carroRiel.transform, "Visual_Carro", PrimitiveType.Cube,
                Vector3.zero, new Vector3(0.85f, 0.28f, 0.85f), trolleyMat);

            // =========================================================================
            // 4. EjeCabestrante (PrismaticJoint - Eje (0, -1, 0) Vertical de izado)
            // =========================================================================
            GameObject ejeCabestrante = CreateArticulationNode("EjeCabestrante", carroRiel.transform, new Vector3(0f, -0.30f, 0f));
            ArticulationBody abCabestrante = ejeCabestrante.GetComponent<ArticulationBody>();
            abCabestrante.jointType = ArticulationJointType.PrismaticJoint;
            abCabestrante.mass = 20f;
            abCabestrante.useGravity = true;
            abCabestrante.matchAnchors = true;
            abCabestrante.anchorPosition = Vector3.zero;
            abCabestrante.anchorRotation = Quaternion.Euler(0f, 0f, -90f);
            abCabestrante.linearLockX = ArticulationDofLock.LimitedMotion;
            abCabestrante.linearLockY = ArticulationDofLock.LockedMotion;
            abCabestrante.linearLockZ = ArticulationDofLock.LockedMotion;
            abCabestrante.xDrive = CreateDrive(
                lowerLimit: 0.0f,
                upperLimit: 4.0f,
                stiffness: enableDrives ? 60000f : 0f,
                damping: enableDrives ? 9000f : 15f,
                forceLimit: enableDrives ? 80000f : 0f,
                target: 0.0f);

            BoxCollider cabestranteCol = ejeCabestrante.AddComponent<BoxCollider>();
            cabestranteCol.center = new Vector3(0f, -0.60f, 0f);
            cabestranteCol.size = new Vector3(0.14f, 1.20f, 0.14f);

            Transform visualCable = AddVisualPrimitive(ejeCabestrante.transform, "Visual_CableRigido", PrimitiveType.Cylinder,
                new Vector3(0f, -0.60f, 0f), new Vector3(0.12f, 0.60f, 0.12f), winchMat);

            // =========================================================================
            // 5. CabezalPinza (SphericalJoint PASIVO - 3 DOF rotacionales sin stiffness)
            // =========================================================================
            GameObject cabezalPinza = CreateArticulationNode("CabezalPinza", ejeCabestrante.transform, new Vector3(0f, -1.30f, 0f));
            ArticulationBody abCabezal = cabezalPinza.GetComponent<ArticulationBody>();
            abCabezal.jointType = ArticulationJointType.SphericalJoint;
            abCabezal.mass = 8f;
            abCabezal.useGravity = true;
            abCabezal.matchAnchors = true;
            abCabezal.anchorPosition = new Vector3(0f, 0.10f, 0f);
            abCabezal.anchorRotation = Quaternion.Euler(0f, 0f, -90f);
            abCabezal.swingYLock = ArticulationDofLock.LimitedMotion;
            abCabezal.swingZLock = ArticulationDofLock.LimitedMotion;
            abCabezal.twistLock = ArticulationDofLock.LimitedMotion;

            ArticulationDrive passiveSwingDrive = CreateDrive(-25f, 25f, stiffness: 0f, damping: 4f, forceLimit: 0f, target: 0f);
            ArticulationDrive passiveTwistDrive = CreateDrive(-30f, 30f, stiffness: 0f, damping: 4f, forceLimit: 0f, target: 0f);
            abCabezal.xDrive = passiveTwistDrive;
            abCabezal.yDrive = passiveSwingDrive;
            abCabezal.zDrive = passiveSwingDrive;

            SphereCollider cabezalCol = cabezalPinza.AddComponent<SphereCollider>();
            cabezalCol.center = Vector3.zero;
            cabezalCol.radius = 0.16f;

            AddVisualPrimitive(cabezalPinza.transform, "Visual_RotulaPasiva", PrimitiveType.Sphere,
                Vector3.zero, new Vector3(0.34f, 0.34f, 0.34f), passiveHeadMat);

            // =========================================================================
            // 6. ChasisGarra (FixedJoint - Solidario al cabezal oscilante)
            // =========================================================================
            GameObject chasisGarra = CreateArticulationNode("ChasisGarra", cabezalPinza.transform, new Vector3(0f, -0.24f, 0f));
            ArticulationBody abChasis = chasisGarra.GetComponent<ArticulationBody>();
            abChasis.jointType = ArticulationJointType.FixedJoint;
            abChasis.mass = 12f;
            abChasis.useGravity = true;
            abChasis.matchAnchors = true;
            abChasis.anchorPosition = new Vector3(0f, 0.12f, 0f);
            abChasis.anchorRotation = Quaternion.identity;

            BoxCollider chasisCol = chasisGarra.AddComponent<BoxCollider>();
            chasisCol.center = Vector3.zero;
            chasisCol.size = new Vector3(1.40f, 0.18f, 0.50f);

            AddVisualPrimitive(chasisGarra.transform, "Visual_ChasisGarra", PrimitiveType.Cube,
                Vector3.zero, new Vector3(1.40f, 0.18f, 0.50f), frameMat);

            // =========================================================================
            // 7. DedoGarra_Izq (PrismaticJoint - Eje (+1, 0, 0) hacia el centro)
            // =========================================================================
            GameObject dedoIzq = CreateArticulationNode("DedoGarra_Izq", chasisGarra.transform, new Vector3(-0.60f, -0.44f, 0f));
            ArticulationBody abDedoIzq = dedoIzq.GetComponent<ArticulationBody>();
            abDedoIzq.jointType = ArticulationJointType.PrismaticJoint;
            abDedoIzq.mass = 5f;
            abDedoIzq.useGravity = true;
            abDedoIzq.matchAnchors = true;
            abDedoIzq.anchorPosition = new Vector3(0f, 0.35f, 0f);
            abDedoIzq.anchorRotation = Quaternion.identity;
            abDedoIzq.linearLockX = ArticulationDofLock.LimitedMotion;
            abDedoIzq.linearLockY = ArticulationDofLock.LockedMotion;
            abDedoIzq.linearLockZ = ArticulationDofLock.LockedMotion;
            abDedoIzq.xDrive = CreateDrive(
                lowerLimit: 0.0f,
                upperLimit: 0.35f,
                stiffness: enableDrives ? 25000f : 0f,
                damping: enableDrives ? 2500f : 10f,
                forceLimit: enableDrives ? 12000f : 0f,
                target: 0.0f);

            BoxCollider dedoIzqCol = dedoIzq.AddComponent<BoxCollider>();
            dedoIzqCol.center = Vector3.zero;
            dedoIzqCol.size = new Vector3(0.14f, 0.70f, 0.52f);
            dedoIzqCol.material = highFrictionMat;

            AddVisualPrimitive(dedoIzq.transform, "Visual_MordazaIzq", PrimitiveType.Cube,
                Vector3.zero, new Vector3(0.14f, 0.70f, 0.52f), fingerMat);

            // =========================================================================
            // 8. DedoGarra_Der (PrismaticJoint - Eje (-1, 0, 0) hacia el centro)
            // =========================================================================
            GameObject dedoDer = CreateArticulationNode("DedoGarra_Der", chasisGarra.transform, new Vector3(0.60f, -0.44f, 0f));
            ArticulationBody abDedoDer = dedoDer.GetComponent<ArticulationBody>();
            abDedoDer.jointType = ArticulationJointType.PrismaticJoint;
            abDedoDer.mass = 5f;
            abDedoDer.useGravity = true;
            abDedoDer.matchAnchors = true;
            abDedoDer.anchorPosition = new Vector3(0f, 0.35f, 0f);
            abDedoDer.anchorRotation = Quaternion.Euler(0f, 180f, 0f);
            abDedoDer.linearLockX = ArticulationDofLock.LimitedMotion;
            abDedoDer.linearLockY = ArticulationDofLock.LockedMotion;
            abDedoDer.linearLockZ = ArticulationDofLock.LockedMotion;
            abDedoDer.xDrive = CreateDrive(
                lowerLimit: 0.0f,
                upperLimit: 0.35f,
                stiffness: enableDrives ? 25000f : 0f,
                damping: enableDrives ? 2500f : 10f,
                forceLimit: enableDrives ? 12000f : 0f,
                target: 0.0f);

            BoxCollider dedoDerCol = dedoDer.AddComponent<BoxCollider>();
            dedoDerCol.center = Vector3.zero;
            dedoDerCol.size = new Vector3(0.14f, 0.70f, 0.52f);
            dedoDerCol.material = highFrictionMat;

            AddVisualPrimitive(dedoDer.transform, "Visual_MordazaDer", PrimitiveType.Cube,
                Vector3.zero, new Vector3(0.14f, 0.70f, 0.52f), fingerMat);

            // Vincular el controlador GravityBatteryController en la raíz de la grúa
            GravityBatteryController controller = baseTorre.AddComponent<GravityBatteryController>();
            controller.brazoPluma = abPluma;
            controller.carroRiel = abCarro;
            controller.ejeCabestrante = abCabestrante;
            controller.dedoIzq = abDedoIzq;
            controller.dedoDer = abDedoDer;
            controller.cabezalPinzaPasivo = abCabezal;
            controller.cableVisualTransform = visualCable;
            controller.drivesActivos = enableDrives;
            controller.targetPlumaGrados = 0f;
            controller.targetCarroMetros = enableDrives ? 2.0f : 0.0f;
            controller.targetCabestranteMetros = 0.0f;
            controller.targetGarraMetros = 0.0f;

            // Crear indicador visual de sombra / proyección vertical debajo de la pinza
            BuildGripperDropIndicator(baseTorre.transform, ejeCabestrante.transform, chasisGarra.transform);

            Selection.activeGameObject = baseTorre;
            return controller;
        }

        private static void BuildGripperDropIndicator(Transform raizGrua, Transform ejeCabestrante, Transform chasisGarra)
        {
            Material shadowOuterMat = GetOrCreateColorMaterial("Mat_SombraPinza_Exterior", new Color(0.08f, 0.12f, 0.16f, 1f));
            Material shadowAccentMat = GetOrCreateColorMaterial("Mat_SombraPinza_Acento", new Color(0.18f, 0.85f, 0.52f, 1f));

            GameObject indicatorRoot = new GameObject("IndicadorSombra_Pinza");
            indicatorRoot.transform.SetParent(raizGrua, false);
            indicatorRoot.transform.localPosition = Vector3.zero;
            indicatorRoot.transform.localRotation = Quaternion.identity;
            indicatorRoot.transform.localScale = Vector3.one;

            GameObject surfaceMarker = new GameObject("MarcadorSuperficie");
            surfaceMarker.transform.SetParent(indicatorRoot.transform, false);

            // Disco oscuro de sombra proyectada en el piso/plataforma (sobresale de la base de 0.60m del bloque)
            Transform discoSombra = AddVisualPrimitive(
                surfaceMarker.transform,
                "Visual_DiscoSombra",
                PrimitiveType.Cylinder,
                Vector3.zero,
                new Vector3(0.95f, 0.006f, 0.95f),
                shadowOuterMat);
            MeshRenderer rSombra = discoSombra.GetComponent<MeshRenderer>();
            if (rSombra != null)
            {
                rSombra.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Marco rectangular ligeramente exterior a la huella del bloque (0.60 x 0.60 m)
            // para que se vea rodeando la base del bloque gris cuando la pinza se alinea encima antes de agarrarlo.
            float grosorLinea = 0.055f;
            float ladoBloque = 0.68f;
            float mitad = ladoBloque * 0.5f;

            Transform bordeNorte = AddVisualPrimitive(surfaceMarker.transform, "Visual_HuellaBloque_N", PrimitiveType.Cube,
                new Vector3(0f, 0.008f, mitad), new Vector3(ladoBloque + grosorLinea, 0.008f, grosorLinea), shadowAccentMat);
            Transform bordeSur = AddVisualPrimitive(surfaceMarker.transform, "Visual_HuellaBloque_S", PrimitiveType.Cube,
                new Vector3(0f, 0.008f, -mitad), new Vector3(ladoBloque + grosorLinea, 0.008f, grosorLinea), shadowAccentMat);
            Transform bordeEste = AddVisualPrimitive(surfaceMarker.transform, "Visual_HuellaBloque_E", PrimitiveType.Cube,
                new Vector3(mitad, 0.008f, 0f), new Vector3(grosorLinea, 0.008f, ladoBloque), shadowAccentMat);
            Transform bordeOeste = AddVisualPrimitive(surfaceMarker.transform, "Visual_HuellaBloque_O", PrimitiveType.Cube,
                new Vector3(-mitad, 0.008f, 0f), new Vector3(grosorLinea, 0.008f, ladoBloque), shadowAccentMat);

            // Punto central de plomada
            Transform nucleoCentral = AddVisualPrimitive(
                surfaceMarker.transform,
                "Visual_PuntoPlomada",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0.010f, 0f),
                new Vector3(0.16f, 0.008f, 0.16f),
                shadowAccentMat);

            foreach (MeshRenderer mr in surfaceMarker.GetComponentsInChildren<MeshRenderer>())
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Línea guía vertical (plomada visual entre la pinza y la sombra)
            LineRenderer line = indicatorRoot.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.startWidth = 0.025f;
            line.endWidth = 0.025f;
            line.positionCount = 2;
            line.sharedMaterial = shadowAccentMat;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            GripperDropIndicator dropIndicator = indicatorRoot.AddComponent<GripperDropIndicator>();
            dropIndicator.raizGrua = raizGrua;
            dropIndicator.ejeCabestrante = ejeCabestrante;
            dropIndicator.chasisGarra = chasisGarra;
            dropIndicator.marcadorSuperficie = surfaceMarker.transform;
            dropIndicator.anilloExterior = discoSombra;
            dropIndicator.nucleoCentral = nucleoCentral;
            dropIndicator.lineaGuiaVertical = line;
            dropIndicator.InicializarCacheColliders();
        }

        // =========================================================================
        // Construcción de la Interfaz HMI Distribuida (Controles Izquierda + Telemetría Derecha)
        // =========================================================================

        private static void BuildTelemetryUI(GravityBatteryController controller, bool enableDrives)
        {
            EnsureEventSystem();

            Font builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject canvasGO = new GameObject(CanvasObjectName);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            GravityBatteryTelemetryUI telemetryUI = canvasGO.AddComponent<GravityBatteryTelemetryUI>();
            telemetryUI.controller = controller;

            Color panelBgColor = new Color(0.06f, 0.08f, 0.11f, 0.90f);
            Color cardBgColor = new Color(0.11f, 0.14f, 0.19f, 0.95f);

            // -------------------------------------------------------------------------
            // BANNER SUPERIOR CENTRAL: Estado del Simulador + Leyenda de Color-Coding
            // -------------------------------------------------------------------------
            GameObject topBanner = CreatePanel(canvasGO.transform, "Banner_Superior",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -14f), new Vector2(860f, 64f), panelBgColor);

            string modoTitulo = enableDrives
                ? "GRÚA BATERÍA DE GRAVEDAD · CONSOLA HMI (Drives Activos)"
                : "MODO CAÍDA LIBRE (Fase 2 · Sin Drives Activos)";
            CreateLabel(topBanner.transform, "TituloSuperior", modoTitulo, builtinFont, 17, FontStyle.Bold,
                new Vector2(0f, -8f), new Vector2(860f, 26f),
                enableDrives ? new Color(0.96f, 0.84f, 0.28f) : new Color(0.95f, 0.45f, 0.35f),
                TextAnchor.MiddleCenter);

            CreateLabel(topBanner.transform, "SubtituloLeyenda",
                "Sincronización Color 3D <-> UI:   [A/D] Pluma (Amarillo)   |   [W/S] Carro (Naranja)   |   [Q/E] Izado (Verde)   |   [Z/X] Mordazas (Rojo)",
                builtinFont, 13, FontStyle.Normal,
                new Vector2(0f, -35f), new Vector2(860f, 22f), new Color(0.80f, 0.86f, 0.92f),
                TextAnchor.MiddleCenter);

            // =========================================================================
            // PANEL IZQUIERDO: CONSOLA DE CONTROLES INTERACTIVOS POR FORMA DE MOVIMIENTO
            // =========================================================================
            GameObject leftPanel = CreatePanel(canvasGO.transform, "Panel_ConsolaMandos_Izq",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -14f), new Vector2(480f, 1052f), panelBgColor);

            CreateLabel(leftPanel.transform, "HeaderControles",
                "MANDOS DE ARTICULACIÓN (Interactivos + Teclado)",
                builtinFont, 15, FontStyle.Bold,
                new Vector2(16f, -12f), new Vector2(448f, 24f), new Color(0.92f, 0.95f, 0.98f));

            // -------------------------------------------------------------------------
            // TARJETA 1: GIRO DE PLUMA (Dial Circular / Radar 360° + Slider Fino)
            // -------------------------------------------------------------------------
            GameObject cardPluma = CreateControlCard(leftPanel.transform, "Card_1_Pluma",
                new Vector2(14f, -44f), new Vector2(452f, 248f), cardBgColor, ColorPluma, out UIControlHoverHighlight hoverPluma);
            telemetryUI.hoverPluma = hoverPluma;

            CreateCardHeader(cardPluma.transform, builtinFont,
                "1. GIRO DE PLUMA (Slewing)", "Revolute Y", "[A] ↺  |  ↻ [D]", ColorPluma);

            // Widget Circular Interactivo (Radar Vista Superior)
            CircularSlewingDialUI dial = CreateCircularSlewingWidget(cardPluma.transform, builtinFont,
                new Vector2(18f, -42f), 148f, ColorPluma);
            telemetryUI.dialPluma = dial;

            // Explicación y referencias de ángulo a la derecha del dial
            CreateLabel(cardPluma.transform, "InfoRadar",
                "Arrastra en círculo el radar\no usa el control fino:",
                builtinFont, 12, FontStyle.Normal,
                new Vector2(180f, -46f), new Vector2(256f, 34f), new Color(0.80f, 0.85f, 0.90f));

            CreateLabel(cardPluma.transform, "RefZonas",
                "▲   0° : Zona de Carga (Suelo)\n◀ -90° : Plataforma Alta (Batería)",
                builtinFont, 12, FontStyle.Bold,
                new Vector2(180f, -84f), new Vector2(256f, 36f), new Color(0.95f, 0.85f, 0.45f));

            // Slider fino horizontal con barra de posición real integrada
            CreateLabel(cardPluma.transform, "ExtremosPluma",
                "↺ -180° (Izq)                        0° (Carga)                        +180° (Der) ↻",
                builtinFont, 11, FontStyle.Normal,
                new Vector2(180f, -132f), new Vector2(256f, 18f), new Color(0.70f, 0.76f, 0.82f));

            telemetryUI.sliderPluma = CreateDualTrackSlider(cardPluma.transform, "Slider_Pluma",
                new Vector2(180f, -154f), new Vector2(256f, 28f),
                -180f, 180f, controller.targetPlumaGrados, ColorPluma, vertical: false,
                out Image fillRealPluma);
            telemetryUI.fillRealPluma = fillRealPluma;

            CreateLabel(cardPluma.transform, "LeyendaBarraPluma",
                "■ Aguja/Barra Amarilla = Target   |   ■ Barra Blanca = Posición Real",
                builtinFont, 11, FontStyle.Italic,
                new Vector2(16f, -214f), new Vector2(420f, 20f), new Color(0.68f, 0.74f, 0.80f));

            // -------------------------------------------------------------------------
            // TARJETA 2: CARRO RIEL RADIAL (Esquema Horizontal de Pluma + Riel Deslizante)
            // -------------------------------------------------------------------------
            GameObject cardCarro = CreateControlCard(leftPanel.transform, "Card_2_CarroRiel",
                new Vector2(14f, -304f), new Vector2(452f, 182f), cardBgColor, ColorCarro, out UIControlHoverHighlight hoverCarro);
            telemetryUI.hoverCarro = hoverCarro;

            CreateCardHeader(cardCarro.transform, builtinFont,
                "2. CARRO RIEL RADIAL (Trolley)", "Prismatic Z", "[S] ◀  |  ▶ [W]", ColorCarro);

            // Esquema visual de la Viga de Pluma con marcas de los 3 bloques de carga
            CreateLabel(cardCarro.transform, "ExtremosCarro",
                "◀ 0.0m (Torre)        0.8m(B3)      2.0m(Carga/Alta)      3.2m(B2)        4.5m (Punta) ▶",
                builtinFont, 11, FontStyle.Bold,
                new Vector2(16f, -46f), new Vector2(420f, 20f), new Color(0.95f, 0.75f, 0.50f));

            // Dibujo esquemático de la viga amarilla detrás del riel del carro
            GameObject vigaEsquema = CreateUIObject("Esquema_VigaPluma", cardCarro.transform);
            RectTransform vigaRT = vigaEsquema.GetComponent<RectTransform>();
            vigaRT.anchorMin = new Vector2(0f, 1f);
            vigaRT.anchorMax = new Vector2(0f, 1f);
            vigaRT.pivot = new Vector2(0f, 1f);
            vigaRT.anchoredPosition = new Vector2(18f, -70f);
            vigaRT.sizeDelta = new Vector2(416f, 8f);
            Image vigaImg = vigaEsquema.AddComponent<Image>();
            vigaImg.color = new Color(ColorPluma.r, ColorPluma.g, ColorPluma.b, 0.45f);

            telemetryUI.sliderCarro = CreateDualTrackSlider(cardCarro.transform, "Slider_Carro",
                new Vector2(18f, -82f), new Vector2(416f, 34f),
                0f, 4.5f, controller.targetCarroMetros, ColorCarro, vertical: false,
                out Image fillRealCarro);
            telemetryUI.fillRealCarro = fillRealCarro;

            // Marcas verticales de referencia en el riel (0.8m, 2.0m, 3.2m)
            CrearMarcaReferenciaRiel(cardCarro.transform, builtinFont, 18f, 416f, 0.8f / 4.5f, -120f, "▲ B3 (0.8m)");
            CrearMarcaReferenciaRiel(cardCarro.transform, builtinFont, 18f, 416f, 2.0f / 4.5f, -120f, "▲ Centro (2.0m)");
            CrearMarcaReferenciaRiel(cardCarro.transform, builtinFont, 18f, 416f, 3.2f / 4.5f, -120f, "▲ B2 (3.2m)");

            CreateLabel(cardCarro.transform, "GuiaCarro",
                "Desliza horizontalmente a lo largo de la pluma ([S] Acercar a Torre / [W] Alejar)",
                builtinFont, 11, FontStyle.Normal,
                new Vector2(16f, -150f), new Vector2(420f, 20f), new Color(0.72f, 0.78f, 0.84f));

            // -------------------------------------------------------------------------
            // TARJETA 3: CABESTRANTE DE IZADO (Columna / Slider VERTICAL de Descenso)
            // -------------------------------------------------------------------------
            GameObject cardCab = CreateControlCard(leftPanel.transform, "Card_3_Cabestrante",
                new Vector2(14f, -498f), new Vector2(452f, 232f), cardBgColor, ColorCabestrante, out UIControlHoverHighlight hoverCab);
            telemetryUI.hoverCabestrante = hoverCab;

            CreateCardHeader(cardCab.transform, builtinFont,
                "3. IZADO CABESTRANTE (Hoist)", "Prismatic -Y", "[Q/↑] ▲ | ▼ [E/↓]", ColorCabestrante);

            // Columna Vertical Interactiva (Arriba = 0.0m Retraído, Abajo = 4.0m Extendido al Suelo)
            telemetryUI.sliderCabestrante = CreateDualTrackSlider(cardCab.transform, "Slider_Cabestrante_Vertical",
                new Vector2(36f, -48f), new Vector2(54f, 166f),
                0f, 4.0f, controller.targetCabestranteMetros, ColorCabestrante, vertical: true,
                out Image fillRealCabestrante);
            telemetryUI.fillRealCabestrante = fillRealCabestrante;

            // Etiquetas de niveles de altura a la derecha de la columna vertical
            CreateLabel(cardCab.transform, "Nivel0",
                "◀ 0.0 m · TOPE SUPERIOR (Cable retraído para traslado) [Q / ↑]",
                builtinFont, 12, FontStyle.Bold,
                new Vector2(104f, -46f), new Vector2(334f, 22f), new Color(0.65f, 0.95f, 0.75f));

            CreateLabel(cardCab.transform, "NivelPlataforma",
                "◀ 1.6 m - 2.2 m · NIVEL PLATAFORMA ALTA (Apilado)",
                builtinFont, 12, FontStyle.Normal,
                new Vector2(104f, -112f), new Vector2(334f, 22f), new Color(0.85f, 0.90f, 0.95f));

            CreateLabel(cardCab.transform, "NivelSuelo",
                "◀ 3.5 m - 3.8 m · NIVEL SUELO (Recoger bloques en Zona Carga) [E / ↓]",
                builtinFont, 12, FontStyle.Bold,
                new Vector2(104f, -188f), new Vector2(334f, 22f), new Color(0.65f, 0.95f, 0.75f));

            CreateLabel(cardCab.transform, "InfoVertical",
                "Control Vertical: Arrastra hacia ABAJO ▼ para descender la pinza\ny hacia ARRIBA ▲ para izar el bloque.",
                builtinFont, 11, FontStyle.Italic,
                new Vector2(104f, -144f), new Vector2(334f, 36f), new Color(0.70f, 0.78f, 0.84f));

            // -------------------------------------------------------------------------
            // TARJETA 4: MORDAZAS DE GARRA (Visualizador Simétrico Bilateral + Botones)
            // -------------------------------------------------------------------------
            GameObject cardGarra = CreateControlCard(leftPanel.transform, "Card_4_MordazasGarra",
                new Vector2(14f, -742f), new Vector2(452f, 294f), cardBgColor, ColorGarra, out UIControlHoverHighlight hoverGarra);
            telemetryUI.hoverGarra = hoverGarra;

            CreateCardHeader(cardGarra.transform, builtinFont,
                "4. MORDAZAS DE GARRA (Grip)", "Prismatic ±X", "[X] ◀▶ | ▶◀ [Z]", ColorGarra);

            // Visualizador gráfico simétrico de las dos mordazas cerrándose sobre el bloque
            CreateSymmetricGripperVisualizer(cardGarra.transform, builtinFont, new Vector2(18f, -44f), new Vector2(416f, 76f),
                ColorGarra,
                out RectTransform mordazaTargetIzq,
                out RectTransform mordazaTargetDer,
                out RectTransform mordazaRealIzq,
                out RectTransform mordazaRealDer,
                out Image imgBloqueCentral,
                out Text txtEstadoAgarre);

            telemetryUI.mordazaTargetIzq = mordazaTargetIzq;
            telemetryUI.mordazaTargetDer = mordazaTargetDer;
            telemetryUI.mordazaRealIzq = mordazaRealIzq;
            telemetryUI.mordazaRealDer = mordazaRealDer;
            telemetryUI.imgBloqueCentralGarra = imgBloqueCentral;
            telemetryUI.txtEstadoAgarre = txtEstadoAgarre;

            CreateLabel(cardGarra.transform, "ExtremosGarra",
                "◀▶ 0.00m (Mordazas Abiertas [X])                     (Prensado Máximo [Z]) 0.35m ▶◀",
                builtinFont, 11, FontStyle.Bold,
                new Vector2(18f, -126f), new Vector2(416f, 18f), new Color(0.95f, 0.70f, 0.68f));

            telemetryUI.sliderGarra = CreateDualTrackSlider(cardGarra.transform, "Slider_Garra",
                new Vector2(18f, -148f), new Vector2(416f, 30f),
                0f, 0.35f, controller.targetGarraMetros, ColorGarra, vertical: false,
                out Image fillRealGarra);
            telemetryUI.fillRealGarra = fillRealGarra;

            // Botones rápidos de maniobra de garra y posición
            telemetryUI.btnCerrarGarra = CreateButton(cardGarra.transform, builtinFont,
                "▶◀ PRENSAR CARGA [Z]", new Vector2(18f, -192f), new Vector2(202f, 40f),
                new Color(0.82f, 0.22f, 0.18f));

            telemetryUI.btnAbrirGarra = CreateButton(cardGarra.transform, builtinFont,
                "◀▶ SOLTAR CARGA [X]", new Vector2(232f, -192f), new Vector2(202f, 40f),
                new Color(0.20f, 0.56f, 0.36f));

            telemetryUI.btnPosicionCarga = CreateButton(cardGarra.transform, builtinFont,
                "⌂ IR A POSICIÓN INICIAL DE CARGA (0°, 2.0m, Arriba)", new Vector2(18f, -240f), new Vector2(416f, 38f),
                new Color(0.22f, 0.42f, 0.68f));

            // =========================================================================
            // PANEL DERECHO: TELEMETRÍA EN VIVO (Target vs. jointPosition + Diana Pasiva)
            // =========================================================================
            GameObject rightPanel = CreatePanel(canvasGO.transform, "Panel_Telemetria_Der",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-16f, -14f), new Vector2(480f, 756f), panelBgColor);

            CreateLabel(rightPanel.transform, "HeaderTelemetria",
                "TELEMETRÍA EN TIEMPO REAL (Target vs. jointPosition)",
                builtinFont, 15, FontStyle.Bold,
                new Vector2(16f, -12f), new Vector2(448f, 24f), new Color(0.45f, 0.88f, 0.98f));

            CreateLabel(rightPanel.transform, "SubHeaderTelemetria",
                "Comparativa de cada ArticulationBody: Barra Color = Target | Barra Blanca = Real",
                builtinFont, 11, FontStyle.Normal,
                new Vector2(16f, -34f), new Vector2(448f, 18f), new Color(0.75f, 0.82f, 0.88f));

            // 5 Tarjetas de Telemetría para los Joints Controlados
            telemetryUI.txtEstadoPluma = CreateTelemetryRowCard(rightPanel.transform, builtinFont,
                "Tel_1_Pluma", -60f, ColorPluma,
                out telemetryUI.barTargetPluma, out telemetryUI.barRealPluma);

            telemetryUI.txtEstadoCarro = CreateTelemetryRowCard(rightPanel.transform, builtinFont,
                "Tel_2_Carro", -148f, ColorCarro,
                out telemetryUI.barTargetCarro, out telemetryUI.barRealCarro);

            telemetryUI.txtEstadoCabestrante = CreateTelemetryRowCard(rightPanel.transform, builtinFont,
                "Tel_3_Cabestrante", -236f, ColorCabestrante,
                out telemetryUI.barTargetCabestrante, out telemetryUI.barRealCabestrante);

            telemetryUI.txtEstadoDedoIzq = CreateTelemetryRowCard(rightPanel.transform, builtinFont,
                "Tel_4_DedoIzq", -324f, ColorGarra,
                out telemetryUI.barTargetDedoIzq, out telemetryUI.barRealDedoIzq);

            telemetryUI.txtEstadoDedoDer = CreateTelemetryRowCard(rightPanel.transform, builtinFont,
                "Tel_5_DedoDer", -412f, ColorGarra,
                out telemetryUI.barTargetDedoDer, out telemetryUI.barRealDedoDer);

            // 6. Tarjeta Especial para el Joint Esférico Pasivo (CabezalPinza) con Diana 2D (Inclinómetro)
            GameObject cardPasivo = CreateControlCard(rightPanel.transform, "Tel_6_CabezalPasivo",
                new Vector2(14f, -504f), new Vector2(452f, 236f), cardBgColor, ColorPasivo, out UIControlHoverHighlight hoverPasivo);
            telemetryUI.hoverPasivo = hoverPasivo;

            CreateCardHeader(cardPasivo.transform, builtinFont,
                "6. CABEZAL PINZA (Rótula Pasiva)", "Spherical 3-DOF", "SIN DRIVE (Gravedad)", ColorPasivo);

            telemetryUI.txtEstadoCabezalPasivo = CreateLabel(cardPasivo.transform, "Txt_Pasivo", "",
                builtinFont, 13, FontStyle.Bold,
                new Vector2(16f, -42f), new Vector2(420f, 24f), new Color(0.65f, 0.95f, 0.98f));

            // Diana / Bullseye 2D de Oscilación Pendular
            telemetryUI.puntoInclinacionPasiva = CreatePassiveSwayBullseye(cardPasivo.transform, builtinFont,
                new Vector2(26f, -74f), 144f, ColorPasivo);
            telemetryUI.radioDianaPasivaPx = 56f;

            CreateLabel(cardPasivo.transform, "InfoPasivo",
                "Inclinómetro Pendular 2D:\n" +
                "• Stiffness = 0 N/m en sus 3 ejes.\n" +
                "• Oscila libremente por inercia al acelerar\n  la pluma o el carro, y al apoyar bloques.\n" +
                "• Pasa el mouse aquí para resaltar la\n  rótula esférica cian en la grúa 3D.",
                builtinFont, 12, FontStyle.Normal,
                new Vector2(188f, -76f), new Vector2(248f, 140f), new Color(0.78f, 0.86f, 0.92f));
        }

        // =========================================================================
        // Constructores de Widgets Especializados (Dial Circular, Sliders Duales, Pinza, Diana)
        // =========================================================================

        private static GameObject CreatePanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size,
            Color bgColor)
        {
            GameObject go = CreateUIObject(name, parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image img = go.AddComponent<Image>();
            img.color = bgColor;
            return go;
        }

        private static GameObject CreateControlCard(
            Transform parent,
            string name,
            Vector2 anchoredPos,
            Vector2 size,
            Color bgColor,
            Color accentColor,
            out UIControlHoverHighlight hoverComponent)
        {
            GameObject cardGO = CreatePanel(parent, name,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                anchoredPos, size, bgColor);

            // Borde superior/inferior sutil para feedback de hover
            GameObject borderGO = CreateUIObject("HighlightBorder", cardGO.transform);
            RectTransform borderRT = borderGO.GetComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.offsetMin = Vector2.zero;
            borderRT.offsetMax = Vector2.zero;
            Image borderImg = borderGO.AddComponent<Image>();
            borderImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.06f);
            borderImg.raycastTarget = false;

            // Franja lateral izquierda del color exacto de la pieza 3D
            GameObject stripeGO = CreateUIObject("ColorStripe", cardGO.transform);
            RectTransform stripeRT = stripeGO.GetComponent<RectTransform>();
            stripeRT.anchorMin = new Vector2(0f, 0f);
            stripeRT.anchorMax = new Vector2(0f, 1f);
            stripeRT.pivot = new Vector2(0f, 0.5f);
            stripeRT.anchoredPosition = Vector2.zero;
            stripeRT.sizeDelta = new Vector2(6f, 0f);
            Image stripeImg = stripeGO.AddComponent<Image>();
            stripeImg.color = accentColor;
            stripeImg.raycastTarget = false;

            hoverComponent = cardGO.AddComponent<UIControlHoverHighlight>();
            hoverComponent.cardBorderImage = borderImg;
            hoverComponent.normalBorderColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.05f);
            hoverComponent.activeBorderColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.24f);

            return cardGO;
        }

        private static void CreateCardHeader(
            Transform cardParent,
            Font font,
            string title,
            string jointBadgeText,
            string keybindText,
            Color accentColor)
        {
            // Pastilla de color junto al título
            GameObject swatch = CreateUIObject("ColorSwatch", cardParent);
            RectTransform swRT = swatch.GetComponent<RectTransform>();
            swRT.anchorMin = new Vector2(0f, 1f);
            swRT.anchorMax = new Vector2(0f, 1f);
            swRT.pivot = new Vector2(0f, 1f);
            swRT.anchoredPosition = new Vector2(16f, -12f);
            swRT.sizeDelta = new Vector2(14f, 14f);
            Image swImg = swatch.AddComponent<Image>();
            swImg.color = accentColor;

            CreateLabel(cardParent, "Title", title, font, 14, FontStyle.Bold,
                new Vector2(36f, -8f), new Vector2(250f, 22f), Color.white);

            // Badge de Teclas
            GameObject keyBadge = CreatePanel(cardParent, "KeyBadge",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-12f, -8f), new Vector2(128f, 22f),
                new Color(accentColor.r * 0.35f, accentColor.g * 0.35f, accentColor.b * 0.35f, 0.95f));
            CreateLabel(keyBadge.transform, "TxtKey", keybindText, font, 11, FontStyle.Bold,
                Vector2.zero, new Vector2(128f, 22f), accentColor, TextAnchor.MiddleCenter);
        }

        private static CircularSlewingDialUI CreateCircularSlewingWidget(
            Transform parent,
            Font font,
            Vector2 anchoredPos,
            float diameter,
            Color accentColor)
        {
            GameObject dialRoot = CreateUIObject("DialCircular_Pluma", parent);
            RectTransform rt = dialRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(diameter, diameter);

            // Fondo circular del radar
            UICircleGraphic bgCircle = dialRoot.AddComponent<UICircleGraphic>();
            bgCircle.isRing = false;
            bgCircle.color = new Color(0.05f, 0.07f, 0.10f, 0.95f);

            // Sector de Zona de Carga (0° Arriba, ±20°)
            GameObject sectorCarga = CreateUIObject("Sector_ZonaCarga", dialRoot.transform);
            RectTransform scRT = sectorCarga.GetComponent<RectTransform>();
            scRT.anchorMin = Vector2.zero;
            scRT.anchorMax = Vector2.one;
            scRT.offsetMin = new Vector2(6f, 6f);
            scRT.offsetMax = new Vector2(-6f, -6f);
            UICircleGraphic scPoly = sectorCarga.AddComponent<UICircleGraphic>();
            scPoly.isRing = false;
            scPoly.startAngleDeg = -22f;
            scPoly.endAngleDeg = 22f;
            scPoly.color = new Color(0.20f, 0.65f, 0.38f, 0.28f);
            scPoly.raycastTarget = false;

            // Sector de Plataforma Alta (-90° Izquierda, -112° a -68°)
            GameObject sectorAlta = CreateUIObject("Sector_PlataformaAlta", dialRoot.transform);
            RectTransform saRT = sectorAlta.GetComponent<RectTransform>();
            saRT.anchorMin = Vector2.zero;
            saRT.anchorMax = Vector2.one;
            saRT.offsetMin = new Vector2(6f, 6f);
            saRT.offsetMax = new Vector2(-6f, -6f);
            UICircleGraphic saPoly = sectorAlta.AddComponent<UICircleGraphic>();
            saPoly.isRing = false;
            saPoly.startAngleDeg = -112f;
            saPoly.endAngleDeg = -68f;
            saPoly.color = new Color(0.25f, 0.55f, 0.85f, 0.28f);
            saPoly.raycastTarget = false;

            // Anillo exterior graduado
            GameObject outerRing = CreateUIObject("OuterRing", dialRoot.transform);
            RectTransform orRT = outerRing.GetComponent<RectTransform>();
            orRT.anchorMin = Vector2.zero;
            orRT.anchorMax = Vector2.one;
            orRT.offsetMin = Vector2.zero;
            orRT.offsetMax = Vector2.zero;
            UICircleGraphic orGraphic = outerRing.AddComponent<UICircleGraphic>();
            orGraphic.isRing = true;
            orGraphic.ringThickness = 4f;
            orGraphic.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.75f);
            orGraphic.raycastTarget = false;

            // Ejes en cruz (Horizontal y Vertical)
            GameObject ejeH = CreateUIObject("EjeH", dialRoot.transform);
            RectTransform ejeHRT = ejeH.GetComponent<RectTransform>();
            ejeHRT.anchorMin = new Vector2(0.5f, 0.5f);
            ejeHRT.anchorMax = new Vector2(0.5f, 0.5f);
            ejeHRT.sizeDelta = new Vector2(diameter - 12f, 1.5f);
            Image ejeHImg = ejeH.AddComponent<Image>();
            ejeHImg.color = new Color(1f, 1f, 1f, 0.14f);
            ejeHImg.raycastTarget = false;

            GameObject ejeV = CreateUIObject("EjeV", dialRoot.transform);
            RectTransform ejeVRT = ejeV.GetComponent<RectTransform>();
            ejeVRT.anchorMin = new Vector2(0.5f, 0.5f);
            ejeVRT.anchorMax = new Vector2(0.5f, 0.5f);
            ejeVRT.sizeDelta = new Vector2(1.5f, diameter - 12f);
            Image ejeVImg = ejeV.AddComponent<Image>();
            ejeVImg.color = new Color(1f, 1f, 1f, 0.14f);
            ejeVImg.raycastTarget = false;

            // Etiquetas cardinales dentro del radar (0° Carga arriba, -90° Torre izq, +90° der)
            CreateCenteredSmallLabel(dialRoot.transform, "Lbl0", "0° CARGA", font, 9,
                new Vector2(0f, diameter * 0.36f), new Color(0.55f, 0.95f, 0.68f));
            CreateCenteredSmallLabel(dialRoot.transform, "LblMinus90", "-90°\nALTA", font, 9,
                new Vector2(-diameter * 0.33f, 0f), new Color(0.55f, 0.85f, 1f));
            CreateCenteredSmallLabel(dialRoot.transform, "LblPlus90", "+90°", font, 9,
                new Vector2(diameter * 0.35f, 0f), new Color(0.75f, 0.78f, 0.82f));
            CreateCenteredSmallLabel(dialRoot.transform, "Lbl180", "±180°", font, 9,
                new Vector2(0f, -diameter * 0.38f), new Color(0.65f, 0.68f, 0.72f));

            // Aguja de Posición Física REAL (Blanca/Cian translúcida, detrás de la aguja Target)
            GameObject pivotReal = CreateUIObject("Pivot_AgujaReal", dialRoot.transform);
            RectTransform pivotRealRT = pivotReal.GetComponent<RectTransform>();
            pivotRealRT.anchorMin = new Vector2(0.5f, 0.5f);
            pivotRealRT.anchorMax = new Vector2(0.5f, 0.5f);
            pivotRealRT.pivot = new Vector2(0.5f, 0.5f);
            pivotRealRT.anchoredPosition = Vector2.zero;
            pivotRealRT.sizeDelta = Vector2.zero;

            GameObject barraReal = CreateUIObject("BarraReal", pivotReal.transform);
            RectTransform brRT = barraReal.GetComponent<RectTransform>();
            brRT.anchorMin = new Vector2(0.5f, 0.5f);
            brRT.anchorMax = new Vector2(0.5f, 0.5f);
            brRT.pivot = new Vector2(0.5f, 0.15f);
            brRT.anchoredPosition = Vector2.zero;
            brRT.sizeDelta = new Vector2(8f, diameter * 0.44f);
            Image brImg = barraReal.AddComponent<Image>();
            brImg.color = new Color(0.85f, 0.96f, 1f, 0.55f);
            brImg.raycastTarget = false;

            // Aguja de Posición TARGET (Amarillo Pluma con perilla en el extremo)
            GameObject pivotTarget = CreateUIObject("Pivot_AgujaTarget", dialRoot.transform);
            RectTransform pivotTargetRT = pivotTarget.GetComponent<RectTransform>();
            pivotTargetRT.anchorMin = new Vector2(0.5f, 0.5f);
            pivotTargetRT.anchorMax = new Vector2(0.5f, 0.5f);
            pivotTargetRT.pivot = new Vector2(0.5f, 0.5f);
            pivotTargetRT.anchoredPosition = Vector2.zero;
            pivotTargetRT.sizeDelta = Vector2.zero;

            GameObject barraTarget = CreateUIObject("BarraTarget", pivotTarget.transform);
            RectTransform btRT = barraTarget.GetComponent<RectTransform>();
            btRT.anchorMin = new Vector2(0.5f, 0.5f);
            btRT.anchorMax = new Vector2(0.5f, 0.5f);
            btRT.pivot = new Vector2(0.5f, 0.15f);
            btRT.anchoredPosition = Vector2.zero;
            btRT.sizeDelta = new Vector2(4f, diameter * 0.46f);
            Image btImg = barraTarget.AddComponent<Image>();
            btImg.color = accentColor;
            btImg.raycastTarget = false;

            GameObject knobTarget = CreateUIObject("PerillaTarget", pivotTarget.transform);
            RectTransform ktRT = knobTarget.GetComponent<RectTransform>();
            ktRT.anchorMin = new Vector2(0.5f, 0.5f);
            ktRT.anchorMax = new Vector2(0.5f, 0.5f);
            ktRT.anchoredPosition = new Vector2(0f, diameter * 0.38f);
            ktRT.sizeDelta = new Vector2(14f, 14f);
            UICircleGraphic ktCircle = knobTarget.AddComponent<UICircleGraphic>();
            ktCircle.isRing = false;
            ktCircle.color = accentColor;
            ktCircle.raycastTarget = false;

            // Núcleo central (Torre) con lectura de ángulo en vivo
            GameObject centroTorre = CreateUIObject("CentroTorre", dialRoot.transform);
            RectTransform ctRT = centroTorre.GetComponent<RectTransform>();
            ctRT.anchorMin = new Vector2(0.5f, 0.5f);
            ctRT.anchorMax = new Vector2(0.5f, 0.5f);
            ctRT.anchoredPosition = Vector2.zero;
            ctRT.sizeDelta = new Vector2(40f, 40f);
            UICircleGraphic ctCircle = centroTorre.AddComponent<UICircleGraphic>();
            ctCircle.isRing = false;
            ctCircle.color = new Color(0.16f, 0.20f, 0.26f, 1f);
            ctCircle.raycastTarget = false;

            Text txtCentral = CreateCenteredSmallLabel(centroTorre.transform, "TxtAngulo", "0°", font, 11,
                Vector2.zero, Color.white);

            CircularSlewingDialUI dialComp = dialRoot.AddComponent<CircularSlewingDialUI>();
            dialComp.dialRect = rt;
            dialComp.agujaTarget = pivotTargetRT;
            dialComp.agujaReal = pivotRealRT;
            dialComp.txtAnguloCentral = txtCentral;

            return dialComp;
        }

        private static Slider CreateDualTrackSlider(
            Transform parent,
            string name,
            Vector2 anchoredPos,
            Vector2 size,
            float minVal,
            float maxVal,
            float initialVal,
            Color accentColor,
            bool vertical,
            out Image realBarImage)
        {
            GameObject sliderGO = CreateUIObject(name, parent);
            RectTransform sliderRT = sliderGO.GetComponent<RectTransform>();
            sliderRT.anchorMin = new Vector2(0f, 1f);
            sliderRT.anchorMax = new Vector2(0f, 1f);
            sliderRT.pivot = new Vector2(0f, 1f);
            sliderRT.anchoredPosition = anchoredPos;
            sliderRT.sizeDelta = size;

            // Fondo del Riel / Columna
            GameObject bgGO = CreateUIObject("TrackBackground", sliderGO.transform);
            RectTransform bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            Image bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.08f, 0.11f, 0.95f);

            // Sub-pista para la Posición Física REAL (jointPosition)
            GameObject realContainerGO = CreateUIObject("RealPositionTrack", sliderGO.transform);
            RectTransform realContRT = realContainerGO.GetComponent<RectTransform>();
            if (!vertical)
            {
                realContRT.anchorMin = new Vector2(0f, 0.08f);
                realContRT.anchorMax = new Vector2(1f, 0.36f);
                realContRT.offsetMin = new Vector2(4f, 0f);
                realContRT.offsetMax = new Vector2(-4f, 0f);
            }
            else
            {
                realContRT.anchorMin = new Vector2(0.08f, 0f);
                realContRT.anchorMax = new Vector2(0.38f, 1f);
                realContRT.offsetMin = new Vector2(0f, 4f);
                realContRT.offsetMax = new Vector2(0f, -4f);
            }

            GameObject realFillGO = CreateUIObject("Fill_Real", realContainerGO.transform);
            RectTransform realFillRT = realFillGO.GetComponent<RectTransform>();
            realFillRT.anchorMin = Vector2.zero;
            realFillRT.anchorMax = Vector2.one;
            realFillRT.offsetMin = Vector2.zero;
            realFillRT.offsetMax = Vector2.zero;
            realBarImage = realFillGO.AddComponent<Image>();
            realBarImage.color = new Color(0.82f, 0.94f, 1f, 0.85f);
            realBarImage.raycastTarget = false;

            // Fill Area del Target (Slider interactivo)
            GameObject fillAreaGO = CreateUIObject("Fill Area", sliderGO.transform);
            RectTransform fillAreaRT = fillAreaGO.GetComponent<RectTransform>();
            if (!vertical)
            {
                fillAreaRT.anchorMin = new Vector2(0f, 0.42f);
                fillAreaRT.anchorMax = new Vector2(1f, 0.88f);
                fillAreaRT.offsetMin = new Vector2(4f, 0f);
                fillAreaRT.offsetMax = new Vector2(-4f, 0f);
            }
            else
            {
                fillAreaRT.anchorMin = new Vector2(0.44f, 0f);
                fillAreaRT.anchorMax = new Vector2(0.90f, 1f);
                fillAreaRT.offsetMin = new Vector2(0f, 4f);
                fillAreaRT.offsetMax = new Vector2(0f, -4f);
            }

            GameObject fillGO = CreateUIObject("Fill_Target", fillAreaGO.transform);
            RectTransform fillRT = fillGO.GetComponent<RectTransform>();
            fillRT.sizeDelta = Vector2.zero;
            Image fillImg = fillGO.AddComponent<Image>();
            fillImg.color = accentColor;

            // Handle Slide Area
            GameObject handleAreaGO = CreateUIObject("Handle Slide Area", sliderGO.transform);
            RectTransform handleAreaRT = handleAreaGO.GetComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero;
            handleAreaRT.anchorMax = Vector2.one;
            if (!vertical)
            {
                handleAreaRT.offsetMin = new Vector2(8f, 0f);
                handleAreaRT.offsetMax = new Vector2(-8f, 0f);
            }
            else
            {
                handleAreaRT.offsetMin = new Vector2(0f, 8f);
                handleAreaRT.offsetMax = new Vector2(0f, -8f);
            }

            GameObject handleGO = CreateUIObject("Handle", handleAreaGO.transform);
            RectTransform handleRT = handleGO.GetComponent<RectTransform>();
            handleRT.sizeDelta = !vertical ? new Vector2(18f, 0f) : new Vector2(0f, 18f);
            Image handleImg = handleGO.AddComponent<Image>();
            handleImg.color = Color.white;

            // Detalle interno del cursor con el color de la pieza
            GameObject handleInner = CreateUIObject("HandleAccent", handleGO.transform);
            RectTransform hiRT = handleInner.GetComponent<RectTransform>();
            hiRT.anchorMin = new Vector2(0.2f, 0.2f);
            hiRT.anchorMax = new Vector2(0.8f, 0.8f);
            hiRT.offsetMin = Vector2.zero;
            hiRT.offsetMax = Vector2.zero;
            Image hiImg = handleInner.AddComponent<Image>();
            hiImg.color = accentColor;
            hiImg.raycastTarget = false;

            Slider slider = sliderGO.AddComponent<Slider>();
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.targetGraphic = handleImg;
            slider.direction = vertical ? Slider.Direction.TopToBottom : Slider.Direction.LeftToRight;
            slider.minValue = minVal;
            slider.maxValue = maxVal;
            slider.value = initialVal;

            return slider;
        }

        private static void CreateSymmetricGripperVisualizer(
            Transform parent,
            Font font,
            Vector2 anchoredPos,
            Vector2 size,
            Color jawColor,
            out RectTransform targetIzq,
            out RectTransform targetDer,
            out RectTransform realIzq,
            out RectTransform realDer,
            out Image centralBlockImg,
            out Text statusLabel)
        {
            GameObject box = CreatePanel(parent, "Visualizador_PinzaSimetrica",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                anchoredPos, size, new Color(0.05f, 0.07f, 0.10f, 0.95f));

            // Sub-área izquierda (esquema mecánico de la pinza)
            GameObject schematicRoot = CreateUIObject("EsquemaPinza", box.transform);
            RectTransform srRT = schematicRoot.GetComponent<RectTransform>();
            srRT.anchorMin = new Vector2(0f, 0f);
            srRT.anchorMax = new Vector2(0f, 1f);
            srRT.pivot = new Vector2(0.5f, 0.5f);
            srRT.anchoredPosition = new Vector2(88f, 0f);
            srRT.sizeDelta = new Vector2(156f, 0f);

            // Chasis superior de la garra
            GameObject chasisTop = CreateUIObject("ChasisSuperior", schematicRoot.transform);
            RectTransform ctRT = chasisTop.GetComponent<RectTransform>();
            ctRT.anchorMin = new Vector2(0.5f, 1f);
            ctRT.anchorMax = new Vector2(0.5f, 1f);
            ctRT.pivot = new Vector2(0.5f, 1f);
            ctRT.anchoredPosition = new Vector2(0f, -8f);
            ctRT.sizeDelta = new Vector2(132f, 10f);
            Image ctImg = chasisTop.AddComponent<Image>();
            ctImg.color = new Color(0.35f, 0.38f, 0.44f, 1f);

            // Bloque central de hormigón (referencia de carga)
            GameObject bloqueGO = CreateUIObject("BloqueHormigonRef", schematicRoot.transform);
            RectTransform blRT = bloqueGO.GetComponent<RectTransform>();
            blRT.anchorMin = new Vector2(0.5f, 0.5f);
            blRT.anchorMax = new Vector2(0.5f, 0.5f);
            blRT.anchoredPosition = new Vector2(0f, -6f);
            blRT.sizeDelta = new Vector2(32f, 38f);
            centralBlockImg = bloqueGO.AddComponent<Image>();
            centralBlockImg.color = new Color(0.55f, 0.58f, 0.62f, 0.45f);

            // Mordazas Fantasma TARGET (indican hasta dónde ordena cerrar el usuario)
            targetIzq = CreateJawRect(schematicRoot.transform, "MordazaTarget_Izq", new Vector2(-58f, -6f),
                new Color(jawColor.r, jawColor.g, jawColor.b, 0.30f));
            targetDer = CreateJawRect(schematicRoot.transform, "MordazaTarget_Der", new Vector2(58f, -6f),
                new Color(jawColor.r, jawColor.g, jawColor.b, 0.30f));

            // Mordazas Físicas REALES (indican dónde están realmente DedoGarra_Izq y DedoGarra_Der)
            realIzq = CreateJawRect(schematicRoot.transform, "MordazaReal_Izq", new Vector2(-58f, -6f), jawColor);
            realDer = CreateJawRect(schematicRoot.transform, "MordazaReal_Der", new Vector2(58f, -6f), jawColor);

            // Etiquetas de estado a la derecha del esquema
            statusLabel = CreateLabel(box.transform, "TxtEstadoAgarre", "○ MORDAZAS ABIERTAS / LIBRES",
                font, 12, FontStyle.Bold,
                new Vector2(174f, -12f), new Vector2(232f, 22f), new Color(0.75f, 0.80f, 0.86f));

            CreateLabel(box.transform, "DescPinza",
                "Vista Frontal Simétrica:\nRojo Sólido = Posición Real\nRojo Translúcido = Target ordenado",
                font, 11, FontStyle.Normal,
                new Vector2(174f, -34f), new Vector2(232f, 38f), new Color(0.68f, 0.74f, 0.80f));
        }

        private static RectTransform CreateJawRect(Transform parent, string name, Vector2 pos, Color color)
        {
            GameObject go = CreateUIObject(name, parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(10f, 44f);
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        private static Text CreateTelemetryRowCard(
            Transform parent,
            Font font,
            string name,
            float yOffset,
            Color pieceColor,
            out Image barTarget,
            out Image barReal)
        {
            GameObject card = CreatePanel(parent, name,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, yOffset), new Vector2(452f, 80f),
                new Color(0.11f, 0.14f, 0.19f, 0.95f));

            // Franja lateral con el color de la pieza
            GameObject stripe = CreateUIObject("ColorStripe", card.transform);
            RectTransform stRT = stripe.GetComponent<RectTransform>();
            stRT.anchorMin = new Vector2(0f, 0f);
            stRT.anchorMax = new Vector2(0f, 1f);
            stRT.pivot = new Vector2(0f, 0.5f);
            stRT.anchoredPosition = Vector2.zero;
            stRT.sizeDelta = new Vector2(6f, 0f);
            Image stImg = stripe.AddComponent<Image>();
            stImg.color = pieceColor;

            Text txtState = CreateLabel(card.transform, "Txt_Estado", "", font, 12, FontStyle.Bold,
                new Vector2(16f, -8f), new Vector2(424f, 22f), Color.white);

            // Pista Barra TARGET
            CreateLabel(card.transform, "LblTarget", "TARGET", font, 10, FontStyle.Bold,
                new Vector2(16f, -34f), new Vector2(54f, 16f), pieceColor);
            barTarget = CreateProgressBarTrack(card.transform, "Track_Target",
                new Vector2(72f, -36f), new Vector2(364f, 12f), pieceColor);

            // Pista Barra REAL (jointPosition)
            CreateLabel(card.transform, "LblReal", "REAL", font, 10, FontStyle.Bold,
                new Vector2(16f, -54f), new Vector2(54f, 16f), new Color(0.75f, 0.94f, 1f));
            barReal = CreateProgressBarTrack(card.transform, "Track_Real",
                new Vector2(72f, -56f), new Vector2(364f, 12f), new Color(0.75f, 0.94f, 1f));

            return txtState;
        }

        private static Image CreateProgressBarTrack(
            Transform parent,
            string name,
            Vector2 anchoredPos,
            Vector2 size,
            Color fillColor)
        {
            GameObject trackGO = CreatePanel(parent, name,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                anchoredPos, size, new Color(0.05f, 0.07f, 0.10f, 0.95f));

            GameObject fillGO = CreateUIObject("Fill", trackGO.transform);
            RectTransform fillRT = fillGO.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            Image fillImg = fillGO.AddComponent<Image>();
            fillImg.color = fillColor;
            return fillImg;
        }

        private static RectTransform CreatePassiveSwayBullseye(
            Transform parent,
            Font font,
            Vector2 anchoredPos,
            float diameter,
            Color accentColor)
        {
            GameObject bullseye = CreateUIObject("Diana_OscilacionPasiva", parent);
            RectTransform rt = bullseye.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(diameter, diameter);

            UICircleGraphic bg = bullseye.AddComponent<UICircleGraphic>();
            bg.isRing = false;
            bg.color = new Color(0.05f, 0.07f, 0.10f, 0.95f);

            // Anillo exterior (±25° límite)
            GameObject outerRing = CreateUIObject("OuterRing", bullseye.transform);
            RectTransform orRT = outerRing.GetComponent<RectTransform>();
            orRT.anchorMin = Vector2.zero;
            orRT.anchorMax = Vector2.one;
            orRT.offsetMin = Vector2.zero;
            orRT.offsetMax = Vector2.zero;
            UICircleGraphic orG = outerRing.AddComponent<UICircleGraphic>();
            orG.isRing = true;
            orG.ringThickness = 3f;
            orG.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.65f);

            // Anillo intermedio (±12.5°)
            GameObject midRing = CreateUIObject("MidRing", bullseye.transform);
            RectTransform mrRT = midRing.GetComponent<RectTransform>();
            mrRT.anchorMin = new Vector2(0.25f, 0.25f);
            mrRT.anchorMax = new Vector2(0.75f, 0.75f);
            mrRT.offsetMin = Vector2.zero;
            mrRT.offsetMax = Vector2.zero;
            UICircleGraphic mrG = midRing.AddComponent<UICircleGraphic>();
            mrG.isRing = true;
            mrG.ringThickness = 1.5f;
            mrG.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.30f);

            // Ejes cruzados
            GameObject axisH = CreateUIObject("AxisH", bullseye.transform);
            RectTransform ahRT = axisH.GetComponent<RectTransform>();
            ahRT.anchorMin = new Vector2(0.5f, 0.5f);
            ahRT.anchorMax = new Vector2(0.5f, 0.5f);
            ahRT.sizeDelta = new Vector2(diameter - 10f, 1.5f);
            axisH.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

            GameObject axisV = CreateUIObject("AxisV", bullseye.transform);
            RectTransform avRT = axisV.GetComponent<RectTransform>();
            avRT.anchorMin = new Vector2(0.5f, 0.5f);
            avRT.anchorMax = new Vector2(0.5f, 0.5f);
            avRT.sizeDelta = new Vector2(1.5f, diameter - 10f);
            axisV.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

            // Punto móvil que representa el vector de inclinación pasiva (SwingY, SwingZ)
            GameObject dotGO = CreateUIObject("PuntoInclinacion", bullseye.transform);
            RectTransform dotRT = dotGO.GetComponent<RectTransform>();
            dotRT.anchorMin = new Vector2(0.5f, 0.5f);
            dotRT.anchorMax = new Vector2(0.5f, 0.5f);
            dotRT.pivot = new Vector2(0.5f, 0.5f);
            dotRT.anchoredPosition = Vector2.zero;
            dotRT.sizeDelta = new Vector2(16f, 16f);
            UICircleGraphic dotCircle = dotGO.AddComponent<UICircleGraphic>();
            dotCircle.isRing = false;
            dotCircle.color = accentColor;

            return dotRT;
        }

        private static void CrearMarcaReferenciaRiel(
            Transform parent,
            Font font,
            float trackStartX,
            float trackWidth,
            float normalizedPos,
            float yOffset,
            string text)
        {
            float x = trackStartX + trackWidth * normalizedPos;
            CreateLabel(parent, $"Ref_{text}", text, font, 10, FontStyle.Normal,
                new Vector2(x - 42f, yOffset), new Vector2(84f, 18f),
                new Color(0.75f, 0.82f, 0.88f), TextAnchor.UpperCenter);
        }

        private static Text CreateCenteredSmallLabel(
            Transform parent,
            string name,
            string text,
            Font font,
            int fontSize,
            Vector2 centerOffset,
            Color color)
        {
            GameObject go = CreateUIObject(name, parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = centerOffset;
            rt.sizeDelta = new Vector2(70f, 28f);

            Text uiText = go.AddComponent<Text>();
            uiText.font = font;
            uiText.text = text;
            uiText.fontSize = fontSize;
            uiText.fontStyle = FontStyle.Bold;
            uiText.color = color;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.raycastTarget = false;
            return uiText;
        }

        private static void EnsureEventSystem()
        {
            EventSystem existingES = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (existingES != null)
            {
                return;
            }

            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();

            Type inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModuleType != null)
            {
                esGO.AddComponent(inputSystemModuleType);
            }
            else
            {
                esGO.AddComponent<StandaloneInputModule>();
            }
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Text CreateLabel(
            Transform parent,
            string name,
            string text,
            Font font,
            int fontSize,
            FontStyle style,
            Vector2 anchoredPos,
            Vector2 size,
            Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            GameObject go = CreateUIObject(name, parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Text uiText = go.AddComponent<Text>();
            uiText.font = font;
            uiText.text = text;
            uiText.fontSize = fontSize;
            uiText.fontStyle = style;
            uiText.color = color;
            uiText.alignment = alignment;
            uiText.raycastTarget = false;
            return uiText;
        }

        private static Button CreateButton(
            Transform parent,
            Font font,
            string text,
            Vector2 anchoredPos,
            Vector2 size,
            Color bgColor)
        {
            GameObject btnGO = CreateUIObject($"Btn_{text}", parent);
            RectTransform rt = btnGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image img = btnGO.AddComponent<Image>();
            img.color = bgColor;

            Button btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = img;

            GameObject txtGO = CreateUIObject("Text", btnGO.transform);
            RectTransform txtRT = txtGO.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero;
            txtRT.offsetMax = Vector2.zero;

            Text uiText = txtGO.AddComponent<Text>();
            uiText.font = font;
            uiText.text = text;
            uiText.fontSize = 12;
            uiText.fontStyle = FontStyle.Bold;
            uiText.color = Color.white;
            uiText.alignment = TextAnchor.MiddleCenter;

            return btn;
        }

        private static void BuildEnvironmentAndPayloads(
            Material groundMat,
            Material platformMat,
            Material blockMat,
            PhysicsMaterial highFrictionMat,
            bool enableDrives)
        {
            GameObject envRoot = new GameObject(EnvironmentObjectName);
            envRoot.transform.position = Vector3.zero;
            envRoot.transform.localScale = Vector3.one;

            // Suelo principal
            GameObject suelo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            suelo.name = "Suelo";
            suelo.transform.SetParent(envRoot.transform, false);
            suelo.transform.position = new Vector3(0f, -0.25f, 0f);
            suelo.transform.localScale = new Vector3(24f, 0.5f, 24f);
            suelo.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

            // Plataforma de Almacenamiento en Altura (donde se apilan los bloques para cargar la batería)
            GameObject torreAlmacenamiento = GameObject.CreatePrimitive(PrimitiveType.Cube);
            torreAlmacenamiento.name = "PlataformaAlmacenamiento_Alta";
            torreAlmacenamiento.transform.SetParent(envRoot.transform, false);
            torreAlmacenamiento.transform.position = new Vector3(-3.5f, 1.1f, 0f);
            torreAlmacenamiento.transform.localScale = new Vector3(1.8f, 2.2f, 1.8f);
            torreAlmacenamiento.GetComponent<MeshRenderer>().sharedMaterial = platformMat;

            // Zona de Carga Base (frente a la grúa en +Z)
            GameObject baseCarga = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseCarga.name = "ZonaCarga_Suelo";
            baseCarga.transform.SetParent(envRoot.transform, false);
            baseCarga.transform.position = new Vector3(0f, 0.025f, 3.5f);
            baseCarga.transform.localScale = new Vector3(2.8f, 0.05f, 2.2f);
            baseCarga.GetComponent<MeshRenderer>().sharedMaterial = platformMat;

            if (!enableDrives)
            {
                CreatePayloadBlock(
                    envRoot.transform,
                    "BloquePrueba_ContactoPasivo",
                    new Vector3(0.28f, 0.40f, 1.5f),
                    blockMat,
                    highFrictionMat);
            }

            // 3 Bloques de Carga (Rigidbody convencionales)
            CreatePayloadBlock(envRoot.transform, "BloqueCarga_1", new Vector3(0f, 0.40f, 3.5f), blockMat, highFrictionMat);
            CreatePayloadBlock(envRoot.transform, "BloqueCarga_2", new Vector3(0f, 0.40f, 4.7f), blockMat, highFrictionMat);
            CreatePayloadBlock(envRoot.transform, "BloqueCarga_3", new Vector3(0f, 0.40f, 2.3f), blockMat, highFrictionMat);
        }

        private static void CreatePayloadBlock(
            Transform parent,
            string name,
            Vector3 position,
            Material material,
            PhysicsMaterial highFrictionMat)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.position = position;
            block.transform.localScale = new Vector3(0.60f, 0.65f, 0.60f);

            MeshRenderer renderer = block.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            BoxCollider col = block.GetComponent<BoxCollider>();
            col.material = highFrictionMat;

            Rigidbody rb = block.AddComponent<Rigidbody>();
            rb.mass = 15f;
            rb.linearDamping = 0.2f;
            rb.angularDamping = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private static GameObject CreateArticulationNode(string name, Transform parent, Vector3 localPos)
        {
            GameObject node = new GameObject(name);
            if (parent != null)
            {
                node.transform.SetParent(parent, false);
            }
            node.transform.localPosition = localPos;
            node.transform.localRotation = Quaternion.identity;
            node.transform.localScale = Vector3.one;

            ArticulationBody ab = node.AddComponent<ArticulationBody>();
            ab.solverIterations = 20;
            ab.solverVelocityIterations = 10;
            return node;
        }

        private static Transform AddVisualPrimitive(
            Transform parent,
            string name,
            PrimitiveType type,
            Vector3 localPos,
            Vector3 localScale,
            Material material)
        {
            GameObject visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            Collider col = visual.GetComponent<Collider>();
            if (col != null)
            {
                UnityEngine.Object.DestroyImmediate(col);
            }

            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPos;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = localScale;

            MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return visual.transform;
        }

        private static ArticulationDrive CreateDrive(
            float lowerLimit,
            float upperLimit,
            float stiffness,
            float damping,
            float forceLimit,
            float target)
        {
            return new ArticulationDrive
            {
                lowerLimit = lowerLimit,
                upperLimit = upperLimit,
                stiffness = stiffness,
                damping = damping,
                forceLimit = forceLimit,
                target = target,
                driveType = ArticulationDriveType.Force
            };
        }

        private static void SetupMainCamera(GravityBatteryController controller)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            cam.fieldOfView = 55f;

            GravityBatteryCameraController camController = cam.GetComponent<GravityBatteryCameraController>();
            if (camController == null)
            {
                camController = cam.gameObject.AddComponent<GravityBatteryCameraController>();
            }

            if (controller != null)
            {
                camController.baseTorre = controller.transform;
                camController.brazoPluma = controller.brazoPluma != null ? controller.brazoPluma.transform : null;
                camController.carroRiel = controller.carroRiel != null ? controller.carroRiel.transform : null;
                camController.ejeCabestrante = controller.ejeCabestrante != null ? controller.ejeCabestrante.transform : null;
            }

            camController.SnapToTarget();
        }

        private static void DestroyExistingIfPresent(string objectName)
        {
            GameObject existing = GameObject.Find(objectName);
            while (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
                existing = GameObject.Find(objectName);
            }
        }

        private static void EnsureMaterialsFolder()
        {
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }
        }

        private static PhysicsMaterial GetOrCreateHighFrictionMaterial()
        {
            string path = $"{MaterialsFolder}/HighFrictionGrip.physicMaterial";
            PhysicsMaterial mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (mat == null)
            {
                mat = new PhysicsMaterial("HighFrictionGrip")
                {
                    staticFriction = 2.5f,
                    dynamicFriction = 2.0f,
                    bounciness = 0.0f,
                    frictionCombine = PhysicsMaterialCombine.Maximum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.staticFriction = 2.5f;
                mat.dynamicFriction = 2.0f;
                mat.bounciness = 0.0f;
                mat.frictionCombine = PhysicsMaterialCombine.Maximum;
                mat.bounceCombine = PhysicsMaterialCombine.Minimum;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        private static Material GetOrCreateColorMaterial(string name, Color color)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }
                mat = new Material(shader);
                mat.SetColor("_BaseColor", color);
                mat.color = color;
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.SetColor("_BaseColor", color);
                mat.color = color;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }
    }
}
