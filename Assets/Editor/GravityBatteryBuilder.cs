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
    /// Soporta Fase 2 (Caída Libre), Fase 3 (Drives Calibrados), Fase 4 (Controlador + UI Telemetría) y Fase 5 (WebGL).
    /// </summary>
    public static class GravityBatteryBuilder
    {
        private const string MaterialsFolder = "Assets/Materials";
        private const string RootObjectName = "GruaBateriaGravedad";
        private const string EnvironmentObjectName = "Entorno_BateriaGravedad";
        private const string CanvasObjectName = "Canvas_TelemetriaBateria";

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
            Debug.Log("[GravityBatteryBuilder] Simulador completo construido (Fases 3 y 4: Drives calibrados + Controlador + UI de Telemetría).");
        }

        [MenuItem("Simulador/Batería de Gravedad/3. Configurar y Compilar Prueba WebGL (Fase 5)", false, 30)]
        public static void ConfigureAndBuildWebGL()
        {
            // Asegurar que la escena actual esté guardada y en Build Settings
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

            // Configuración recomendada para itch.io (evita errores de cabeceras de compresión en servidores web)
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

            // Eliminar instancias previas si existen
            DestroyExistingIfPresent(RootObjectName);
            DestroyExistingIfPresent(EnvironmentObjectName);
            DestroyExistingIfPresent(CanvasObjectName);

            // Crear materiales visuales y material físico de alta fricción
            PhysicsMaterial highFrictionMat = GetOrCreateHighFrictionMaterial();
            Material towerMat = GetOrCreateColorMaterial("Mat_Torre", new Color(0.22f, 0.25f, 0.28f));
            Material boomMat = GetOrCreateColorMaterial("Mat_Pluma", new Color(0.92f, 0.72f, 0.12f));
            Material trolleyMat = GetOrCreateColorMaterial("Mat_Carro", new Color(0.90f, 0.42f, 0.10f));
            Material winchMat = GetOrCreateColorMaterial("Mat_Cabestrante", new Color(0.65f, 0.68f, 0.72f));
            Material passiveHeadMat = GetOrCreateColorMaterial("Mat_CabezalPasivo", new Color(0.15f, 0.68f, 0.88f));
            Material frameMat = GetOrCreateColorMaterial("Mat_ChasisGarra", new Color(0.28f, 0.30f, 0.34f));
            Material fingerMat = GetOrCreateColorMaterial("Mat_DedoGarra", new Color(0.85f, 0.22f, 0.18f));
            Material blockMat = GetOrCreateColorMaterial("Mat_BloqueCarga", new Color(0.52f, 0.55f, 0.58f));
            Material groundMat = GetOrCreateColorMaterial("Mat_Suelo", new Color(0.16f, 0.18f, 0.20f));
            Material platformMat = GetOrCreateColorMaterial("Mat_Plataforma", new Color(0.20f, 0.42f, 0.30f));

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

            // 3. Construir la Interfaz Gráfica (4 Sliders, Botones y Telemetría Target vs Real)
            BuildTelemetryUI(controller, enableDrives);

            // 4. Posicionar cámara principal
            SetupMainCamera();

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
            // Rotamos el ancla +90° en Z para que su eje X primario apunte hacia (0, 1, 0)
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
            // Rotamos el ancla -90° en Y para que su eje +X primario apunte hacia (0, 0, 1) (+Z)
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
            // Rotamos el ancla -90° en Z para que su eje +X primario apunte hacia (0, -1, 0) (abajo)
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

            // Drives en 0 de stiffness (estrictamente PASIVO: oscila por gravedad, inercia o contacto)
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
            abDedoIzq.anchorRotation = Quaternion.identity; // +X apunta hacia (+1, 0, 0)
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
            abDedoDer.anchorRotation = Quaternion.Euler(0f, 180f, 0f); // +X del ancla apunta hacia (-1, 0, 0)
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

            Selection.activeGameObject = baseTorre;
            return controller;
        }

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
            canvasGO.AddComponent<GraphicRaycaster>();

            GravityBatteryTelemetryUI telemetryUI = canvasGO.AddComponent<GravityBatteryTelemetryUI>();
            telemetryUI.controller = controller;

            // Panel lateral izquierdo
            GameObject panelGO = CreateUIObject("Panel_ControlYTelemetria", canvasGO.transform);
            Image panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.10f, 0.13f, 0.88f);
            RectTransform panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(20f, -20f);
            panelRect.sizeDelta = new Vector2(560f, 680f);

            string modoTitulo = enableDrives
                ? "SIMULADOR BATERÍA DE GRAVEDAD (Drives Activos)"
                : "MODO CAÍDA LIBRE (Fase 2 - Sin Drives)";
            CreateLabel(panelGO.transform, "Titulo", modoTitulo, builtinFont, 20, FontStyle.Bold,
                new Vector2(20f, -18f), new Vector2(520f, 32f), new Color(0.95f, 0.80f, 0.25f));

            CreateLabel(panelGO.transform, "Subtitulo",
                "Teclado: [A/D] Pluma | [W/S] Carro | [Q/E] Izado | [Z/X] Garra",
                builtinFont, 14, FontStyle.Normal,
                new Vector2(20f, -48f), new Vector2(520f, 24f), new Color(0.75f, 0.82f, 0.88f));

            // 4 Sliders de control
            telemetryUI.sliderPluma = CreateLabeledSlider(panelGO.transform, builtinFont,
                "Slider 1 · Giro Pluma [-180° a +180°]", -180f, 180f, controller.targetPlumaGrados, -85f);

            telemetryUI.sliderCarro = CreateLabeledSlider(panelGO.transform, builtinFont,
                "Slider 2 · Carro Riel Radial [0.0 a 4.5 m]", 0f, 4.5f, controller.targetCarroMetros, -145f);

            telemetryUI.sliderCabestrante = CreateLabeledSlider(panelGO.transform, builtinFont,
                "Slider 3 · Izado Cabestrante [0.0 a 4.0 m]", 0f, 4.0f, controller.targetCabestranteMetros, -205f);

            telemetryUI.sliderGarra = CreateLabeledSlider(panelGO.transform, builtinFont,
                "Slider 4 · Presión Mordazas [0.0 a 0.35 m]", 0f, 0.35f, controller.targetGarraMetros, -265f);

            // Botones de acción (2 obligatorios + 1 de retorno)
            telemetryUI.btnCerrarGarra = CreateButton(panelGO.transform, builtinFont,
                "PRENSAR CARGA (Cerrar)", new Vector2(20f, -335f), new Vector2(165f, 40f), new Color(0.82f, 0.28f, 0.22f));

            telemetryUI.btnAbrirGarra = CreateButton(panelGO.transform, builtinFont,
                "SOLTAR CARGA (Abrir)", new Vector2(197f, -335f), new Vector2(165f, 40f), new Color(0.22f, 0.55f, 0.35f));

            telemetryUI.btnPosicionCarga = CreateButton(panelGO.transform, builtinFont,
                "POSICIÓN INICIAL", new Vector2(374f, -335f), new Vector2(165f, 40f), new Color(0.25f, 0.42f, 0.65f));

            // Sección de Lectura de Estado (Target vs Real por joint)
            CreateLabel(panelGO.transform, "HeaderTelemetria",
                "LECTURA DE ESTADO EN TIEMPO REAL (Target vs. jointPosition):",
                builtinFont, 15, FontStyle.Bold,
                new Vector2(20f, -395f), new Vector2(520f, 26f), new Color(0.40f, 0.85f, 0.98f));

            telemetryUI.txtEstadoPluma = CreateLabel(panelGO.transform, "Txt_Pluma", "", builtinFont, 14, FontStyle.Normal,
                new Vector2(20f, -430f), new Vector2(520f, 26f), Color.white);
            telemetryUI.txtEstadoCarro = CreateLabel(panelGO.transform, "Txt_Carro", "", builtinFont, 14, FontStyle.Normal,
                new Vector2(20f, -462f), new Vector2(520f, 26f), Color.white);
            telemetryUI.txtEstadoCabestrante = CreateLabel(panelGO.transform, "Txt_Cabestrante", "", builtinFont, 14, FontStyle.Normal,
                new Vector2(20f, -494f), new Vector2(520f, 26f), Color.white);
            telemetryUI.txtEstadoDedoIzq = CreateLabel(panelGO.transform, "Txt_DedoIzq", "", builtinFont, 14, FontStyle.Normal,
                new Vector2(20f, -526f), new Vector2(520f, 26f), Color.white);
            telemetryUI.txtEstadoDedoDer = CreateLabel(panelGO.transform, "Txt_DedoDer", "", builtinFont, 14, FontStyle.Normal,
                new Vector2(20f, -558f), new Vector2(520f, 26f), Color.white);
            telemetryUI.txtEstadoCabezalPasivo = CreateLabel(panelGO.transform, "Txt_Pasivo", "", builtinFont, 14, FontStyle.Italic,
                new Vector2(20f, -595f), new Vector2(520f, 26f), new Color(0.55f, 0.92f, 0.70f));
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
            Color color)
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
            uiText.alignment = TextAnchor.MiddleLeft;
            return uiText;
        }

        private static Slider CreateLabeledSlider(
            Transform parent,
            Font font,
            string labelText,
            float minVal,
            float maxVal,
            float initialVal,
            float yOffset)
        {
            CreateLabel(parent, $"Lbl_{labelText}", labelText, font, 14, FontStyle.Normal,
                new Vector2(20f, yOffset), new Vector2(520f, 22f), new Color(0.90f, 0.92f, 0.95f));

            GameObject sliderGO = CreateUIObject($"Slider_{labelText}", parent);
            RectTransform sliderRT = sliderGO.GetComponent<RectTransform>();
            sliderRT.anchorMin = new Vector2(0f, 1f);
            sliderRT.anchorMax = new Vector2(0f, 1f);
            sliderRT.pivot = new Vector2(0f, 1f);
            sliderRT.anchoredPosition = new Vector2(20f, yOffset - 24f);
            sliderRT.sizeDelta = new Vector2(520f, 22f);

            // Background
            GameObject bgGO = CreateUIObject("Background", sliderGO.transform);
            RectTransform bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0f, 0.25f);
            bgRT.anchorMax = new Vector2(1f, 0.75f);
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            Image bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.22f, 0.25f, 0.30f, 1f);

            // Fill Area
            GameObject fillAreaGO = CreateUIObject("Fill Area", sliderGO.transform);
            RectTransform fillAreaRT = fillAreaGO.GetComponent<RectTransform>();
            fillAreaRT.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRT.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRT.offsetMin = new Vector2(5f, 0f);
            fillAreaRT.offsetMax = new Vector2(-5f, 0f);

            GameObject fillGO = CreateUIObject("Fill", fillAreaGO.transform);
            RectTransform fillRT = fillGO.GetComponent<RectTransform>();
            fillRT.sizeDelta = new Vector2(10f, 0f);
            Image fillImg = fillGO.AddComponent<Image>();
            fillImg.color = new Color(0.92f, 0.65f, 0.15f, 1f);

            // Handle Slide Area
            GameObject handleAreaGO = CreateUIObject("Handle Slide Area", sliderGO.transform);
            RectTransform handleAreaRT = handleAreaGO.GetComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero;
            handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.offsetMin = new Vector2(10f, 0f);
            handleAreaRT.offsetMax = new Vector2(-10f, 0f);

            GameObject handleGO = CreateUIObject("Handle", handleAreaGO.transform);
            RectTransform handleRT = handleGO.GetComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(18f, 0f);
            Image handleImg = handleGO.AddComponent<Image>();
            handleImg.color = Color.white;

            Slider slider = sliderGO.AddComponent<Slider>();
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = minVal;
            slider.maxValue = maxVal;
            slider.value = initialVal;

            return slider;
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
            uiText.fontSize = 13;
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

            // En Fase 2 (Caída Libre), bloque de contacto pasivo en z = 1.5m
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

        private static void SetupMainCamera()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(7.5f, 6.2f, -5.5f);
                cam.transform.LookAt(new Vector3(-0.5f, 3.0f, 1.5f));
            }
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
            return mat;
        }
    }
}
