using FisicaAvanzada.Mechanics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FisicaAvanzada.UI
{
    /// <summary>
    /// Interfaz HUD distribuida de control espacial y telemetría en tiempo real (Target vs. Valor Real por joint).
    /// Cumple y expande los requisitos de cátedra:
    /// - 4 Controles con forma adaptada al movimiento físico (Dial Circular 360°, Riel Radial, Columna Vertical de Izado y Mordazas Simétricas)
    /// - Sincronización por código de colores (Color-Coding) entre piezas 3D y controles UI
    /// - Telemetría comparativa (Target vs. jointPosition) con barras duales e inclinómetro 2D para el joint pasivo
    /// - Resaltado visual 3D de la pieza activa al operar por UI o teclado
    /// </summary>
    public class GravityBatteryTelemetryUI : MonoBehaviour
    {
        public GravityBatteryController controller;

        [Header("Sliders de Entrada (4 Controles Principales)")]
        public Slider sliderPluma;
        public Slider sliderCarro;
        public Slider sliderCabestrante;
        public Slider sliderGarra;

        [Header("Control Circular Interactivo (Giro de Pluma)")]
        public CircularSlewingDialUI dialPluma;

        [Header("Botones de Maniobra")]
        public Button btnCerrarGarra;
        public Button btnAbrirGarra;
        public Button btnPosicionCarga;

        [Header("Indicadores de Posición Física Real en Controles (Panel Izquierdo)")]
        public Image fillRealPluma;
        public Image fillRealCarro;
        public Image fillRealCabestrante;
        public Image fillRealGarra;

        [Header("Visualizador Simétrico de Mordazas (Pinza Bilateral)")]
        public RectTransform mordazaTargetIzq;
        public RectTransform mordazaTargetDer;
        public RectTransform mordazaRealIzq;
        public RectTransform mordazaRealDer;
        public Image imgBloqueCentralGarra;
        public Text txtEstadoAgarre;

        [Header("Textos de Lectura de Estado (Target vs Real - Panel Derecho)")]
        public Text txtEstadoPluma;
        public Text txtEstadoCarro;
        public Text txtEstadoCabestrante;
        public Text txtEstadoDedoIzq;
        public Text txtEstadoDedoDer;
        public Text txtEstadoCabezalPasivo;

        [Header("Barras Comparativas en Panel de Telemetría (Target vs Real)")]
        public Image barTargetPluma;
        public Image barRealPluma;
        public Image barTargetCarro;
        public Image barRealCarro;
        public Image barTargetCabestrante;
        public Image barRealCabestrante;
        public Image barTargetDedoIzq;
        public Image barRealDedoIzq;
        public Image barTargetDedoDer;
        public Image barRealDedoDer;

        [Header("Inclinómetro 2D de Oscilación Pasiva (SphericalJoint)")]
        public RectTransform puntoInclinacionPasiva;
        public float radioDianaPasivaPx = 38f;

        [Header("Detectores de Hover en Tarjetas para Resaltado 3D")]
        public UIControlHoverHighlight hoverPluma;
        public UIControlHoverHighlight hoverCarro;
        public UIControlHoverHighlight hoverCabestrante;
        public UIControlHoverHighlight hoverGarra;
        public UIControlHoverHighlight hoverPasivo;

        private bool _sincronizandoUI;
        private MaterialPropertyBlock _mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Start()
        {
            _mpb = new MaterialPropertyBlock();

            if (controller == null)
            {
                controller = FindFirstObjectByType<GravityBatteryController>();
            }

            ConfigurarEventosUI();
            ActualizarInteractividadControles();
        }

        private void ActualizarInteractividadControles()
        {
            bool activo = controller == null || controller.drivesActivos;
            if (sliderPluma != null) sliderPluma.interactable = activo;
            if (sliderCarro != null) sliderCarro.interactable = activo;
            if (sliderCabestrante != null) sliderCabestrante.interactable = activo;
            if (sliderGarra != null) sliderGarra.interactable = activo;
            if (dialPluma != null) dialPluma.interactable = activo;
            if (btnCerrarGarra != null) btnCerrarGarra.interactable = activo;
            if (btnAbrirGarra != null) btnAbrirGarra.interactable = activo;
            if (btnPosicionCarga != null) btnPosicionCarga.interactable = activo;
        }

        private void ConfigurarEventosUI()
        {
            if (sliderPluma != null)
            {
                sliderPluma.minValue = -180f;
                sliderPluma.maxValue = 180f;
                sliderPluma.value = controller != null ? controller.targetPlumaGrados : 0f;
                sliderPluma.onValueChanged.AddListener(val =>
                {
                    if (!_sincronizandoUI && controller != null)
                        controller.SetTargetPluma(val);
                });
            }

            if (dialPluma != null)
            {
                dialPluma.OnAngleChanged += angulo =>
                {
                    if (!_sincronizandoUI && controller != null)
                        controller.SetTargetPluma(angulo);
                };
            }

            if (sliderCarro != null)
            {
                sliderCarro.minValue = 0f;
                sliderCarro.maxValue = 4.5f;
                sliderCarro.value = controller != null ? controller.targetCarroMetros : 2.0f;
                sliderCarro.onValueChanged.AddListener(val =>
                {
                    if (!_sincronizandoUI && controller != null)
                        controller.SetTargetCarro(val);
                });
            }

            if (sliderCabestrante != null)
            {
                sliderCabestrante.minValue = 0f;
                sliderCabestrante.maxValue = 4.0f;
                sliderCabestrante.value = controller != null ? controller.targetCabestranteMetros : 0f;
                sliderCabestrante.onValueChanged.AddListener(val =>
                {
                    if (!_sincronizandoUI && controller != null)
                        controller.SetTargetCabestrante(val);
                });
            }

            if (sliderGarra != null)
            {
                sliderGarra.minValue = 0f;
                sliderGarra.maxValue = 0.35f;
                sliderGarra.value = controller != null ? controller.targetGarraMetros : 0f;
                sliderGarra.onValueChanged.AddListener(val =>
                {
                    if (!_sincronizandoUI && controller != null)
                        controller.SetTargetGarra(val);
                });
            }

            if (btnCerrarGarra != null)
            {
                btnCerrarGarra.onClick.AddListener(() =>
                {
                    if (controller != null)
                        controller.CerrarGarraCompleta();
                });
            }

            if (btnAbrirGarra != null)
            {
                btnAbrirGarra.onClick.AddListener(() =>
                {
                    if (controller != null)
                        controller.AbrirGarraCompleta();
                });
            }

            if (btnPosicionCarga != null)
            {
                btnPosicionCarga.onClick.AddListener(() =>
                {
                    if (controller != null)
                        controller.IrAPosicionCarga();
                });
            }
        }

        private void Update()
        {
            if (controller == null)
            {
                return;
            }

            // Sincronizar posición de los Sliders si el usuario mueve con el teclado o con el dial circular
            _sincronizandoUI = true;
            if (sliderPluma != null) sliderPluma.SetValueWithoutNotify(controller.targetPlumaGrados);
            if (sliderCarro != null) sliderCarro.SetValueWithoutNotify(controller.targetCarroMetros);
            if (sliderCabestrante != null) sliderCabestrante.SetValueWithoutNotify(controller.targetCabestranteMetros);
            if (sliderGarra != null) sliderGarra.SetValueWithoutNotify(controller.targetGarraMetros);
            _sincronizandoUI = false;

            ActualizarTelemetriaYVisuales();
            ActualizarResaltado3D();
        }

        private void ActualizarTelemetriaYVisuales()
        {
            float realPluma = controller.ObtenerPosicionRealGrados(controller.brazoPluma);
            float realCarro = controller.ObtenerPosicionRealMetros(controller.carroRiel);
            float realCabestrante = controller.ObtenerPosicionRealMetros(controller.ejeCabestrante);
            float realDedoIzq = controller.ObtenerPosicionRealMetros(controller.dedoIzq);
            float realDedoDer = controller.ObtenerPosicionRealMetros(controller.dedoDer);
            Vector2 inclinacionPasiva = controller.ObtenerInclinacionPasivaGrados();

            float normTargetPluma = Mathf.InverseLerp(-180f, 180f, controller.targetPlumaGrados);
            float normRealPluma = Mathf.InverseLerp(-180f, 180f, realPluma);

            float normTargetCarro = Mathf.InverseLerp(0f, 4.5f, controller.targetCarroMetros);
            float normRealCarro = Mathf.InverseLerp(0f, 4.5f, realCarro);

            float normTargetCab = Mathf.InverseLerp(0f, 4.0f, controller.targetCabestranteMetros);
            float normRealCab = Mathf.InverseLerp(0f, 4.0f, realCabestrante);

            float normTargetGarra = Mathf.InverseLerp(0f, 0.35f, controller.targetGarraMetros);
            float normRealIzq = Mathf.InverseLerp(0f, 0.35f, realDedoIzq);
            float normRealDer = Mathf.InverseLerp(0f, 0.35f, realDedoDer);
            float normRealGarraProm = (normRealIzq + normRealDer) * 0.5f;

            // 1. Actualizar Dial Circular de Pluma y barra real en control
            if (dialPluma != null)
            {
                dialPluma.ActualizarVisuales(controller.targetPlumaGrados, realPluma);
            }
            SetBarProgress(fillRealPluma, normRealPluma, vertical: false);
            SetBarProgress(fillRealCarro, normRealCarro, vertical: false);
            SetBarProgress(fillRealCabestrante, normRealCab, vertical: true);
            SetBarProgress(fillRealGarra, normRealGarraProm, vertical: false);

            // 2. Actualizar Visualizador Simétrico de Mordazas (Pinza Bilateral)
            ActualizarVisualizadorGarra(normTargetGarra, normRealIzq, normRealDer, realDedoIzq, realDedoDer);

            // 3. Actualizar Textos de Estado (Target vs Real)
            if (txtEstadoPluma != null)
            {
                float diff = Mathf.Abs(controller.targetPlumaGrados - realPluma);
                txtEstadoPluma.text = $"1. BrazoPluma (Revolute Y)   | Target: {controller.targetPlumaGrados,6:F1}° | Real: {realPluma,6:F1}° (Δ {diff:F1}°)";
            }

            if (txtEstadoCarro != null)
            {
                float diff = Mathf.Abs(controller.targetCarroMetros - realCarro);
                txtEstadoCarro.text = $"2. CarroRiel (Prismatic Z)   | Target: {controller.targetCarroMetros,5:F2} m | Real: {realCarro,5:F2} m (Δ {diff:F2}m)";
            }

            if (txtEstadoCabestrante != null)
            {
                float diff = Mathf.Abs(controller.targetCabestranteMetros - realCabestrante);
                txtEstadoCabestrante.text = $"3. Cabestrante (Prism. -Y)   | Target: {controller.targetCabestranteMetros,5:F2} m | Real: {realCabestrante,5:F2} m (Δ {diff:F2}m)";
            }

            if (txtEstadoDedoIzq != null)
            {
                txtEstadoDedoIzq.text = $"4. DedoGarra_Izq (Prism +X)  | Target: {controller.targetGarraMetros,5:F2} m | Real: {realDedoIzq,5:F2} m";
            }

            if (txtEstadoDedoDer != null)
            {
                txtEstadoDedoDer.text = $"5. DedoGarra_Der (Prism -X)  | Target: {controller.targetGarraMetros,5:F2} m | Real: {realDedoDer,5:F2} m";
            }

            if (txtEstadoCabezalPasivo != null)
            {
                txtEstadoCabezalPasivo.text = $"6. CabezalPinza (Spherical PASIVO) | Oscilación: ({inclinacionPasiva.x,5:F1}°, {inclinacionPasiva.y,5:F1}°)";
            }

            // 4. Actualizar Barras Comparativas en Panel Derecho (Target vs Real)
            SetBarProgress(barTargetPluma, normTargetPluma, vertical: false);
            SetBarProgress(barRealPluma, normRealPluma, vertical: false);

            SetBarProgress(barTargetCarro, normTargetCarro, vertical: false);
            SetBarProgress(barRealCarro, normRealCarro, vertical: false);

            SetBarProgress(barTargetCabestrante, normTargetCab, vertical: false);
            SetBarProgress(barRealCabestrante, normRealCab, vertical: false);

            SetBarProgress(barTargetDedoIzq, normTargetGarra, vertical: false);
            SetBarProgress(barRealDedoIzq, normRealIzq, vertical: false);

            SetBarProgress(barTargetDedoDer, normTargetGarra, vertical: false);
            SetBarProgress(barRealDedoDer, normRealDer, vertical: false);

            // 5. Actualizar Diana 2D del Joint Esférico Pasivo (rango ±25°)
            if (puntoInclinacionPasiva != null)
            {
                float normX = Mathf.Clamp(inclinacionPasiva.y / 25f, -1f, 1f);
                float normY = Mathf.Clamp(inclinacionPasiva.x / 25f, -1f, 1f);
                puntoInclinacionPasiva.anchoredPosition = new Vector2(normX * radioDianaPasivaPx, normY * radioDianaPasivaPx);
            }
        }

        private void ActualizarVisualizadorGarra(
            float normTarget,
            float normRealIzq,
            float normRealDer,
            float realIzqMetros,
            float realDerMetros)
        {
            // Desplazamiento máximo en píxeles desde el borde exterior hacia el bloque central
            const float xAbierta = 58f;
            const float xCerrada = 20f;

            if (mordazaTargetIzq != null)
            {
                float x = Mathf.Lerp(-xAbierta, -xCerrada, normTarget);
                mordazaTargetIzq.anchoredPosition = new Vector2(x, 0f);
            }

            if (mordazaTargetDer != null)
            {
                float x = Mathf.Lerp(xAbierta, xCerrada, normTarget);
                mordazaTargetDer.anchoredPosition = new Vector2(x, 0f);
            }

            if (mordazaRealIzq != null)
            {
                float x = Mathf.Lerp(-xAbierta, -xCerrada, normRealIzq);
                mordazaRealIzq.anchoredPosition = new Vector2(x, 0f);
            }

            if (mordazaRealDer != null)
            {
                float x = Mathf.Lerp(xAbierta, xCerrada, normRealDer);
                mordazaRealDer.anchoredPosition = new Vector2(x, 0f);
            }

            // Detectar si las mordazas están ejerciendo fuerza de compresión sobre un bloque
            // (Target ordena cerrar más, pero la posición física real se detuvo contra las caras del bloque ~0.16m)
            float realProm = (realIzqMetros + realDerMetros) * 0.5f;
            float errorCompresion = controller.targetGarraMetros - realProm;
            bool prensandoBloque = controller.targetGarraMetros > 0.20f && realProm > 0.08f && realProm < 0.28f && errorCompresion > 0.03f;

            if (imgBloqueCentralGarra != null)
            {
                imgBloqueCentralGarra.color = prensandoBloque
                    ? new Color(0.25f, 0.92f, 0.48f, 0.95f)
                    : new Color(0.55f, 0.58f, 0.62f, 0.45f);
            }

            if (txtEstadoAgarre != null)
            {
                if (prensandoBloque)
                {
                    txtEstadoAgarre.text = "● PRENSANDO BLOQUE (Fricción Activa)";
                    txtEstadoAgarre.color = new Color(0.30f, 0.95f, 0.52f);
                }
                else if (realProm > 0.29f)
                {
                    txtEstadoAgarre.text = "● CERRADA EN VACÍO (Sin Bloque)";
                    txtEstadoAgarre.color = new Color(0.95f, 0.75f, 0.25f);
                }
                else
                {
                    txtEstadoAgarre.text = "○ MORDAZAS ABIERTAS / LIBRES";
                    txtEstadoAgarre.color = new Color(0.70f, 0.76f, 0.82f);
                }
            }
        }

        private static void SetBarProgress(Image barImage, float normalizedValue, bool vertical)
        {
            if (barImage == null)
            {
                return;
            }

            normalizedValue = Mathf.Clamp01(normalizedValue);
            RectTransform rt = barImage.rectTransform;
            if (!vertical)
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(normalizedValue, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                // De arriba hacia abajo (TopToBottom: 0m arriba -> 4m abajo)
                rt.anchorMin = new Vector2(0f, 1f - normalizedValue);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }

        private void ActualizarResaltado3D()
        {
            if (_mpb == null)
            {
                _mpb = new MaterialPropertyBlock();
            }

            Keyboard kb = Keyboard.current;
            bool teclaPluma = kb != null && (kb.aKey.isPressed || kb.dKey.isPressed);
            bool teclaCarro = kb != null && (kb.wKey.isPressed || kb.sKey.isPressed);
            bool teclaCab = kb != null && (kb.qKey.isPressed || kb.eKey.isPressed || kb.upArrowKey.isPressed || kb.downArrowKey.isPressed);
            bool teclaGarra = kb != null && (kb.zKey.isPressed || kb.xKey.isPressed);

            bool activoPluma = teclaPluma
                || (hoverPluma != null && hoverPluma.IsActive)
                || (dialPluma != null && (dialPluma.IsHovered || dialPluma.IsDragging));
            bool activoCarro = teclaCarro || (hoverCarro != null && hoverCarro.IsActive);
            bool activoCab = teclaCab || (hoverCabestrante != null && hoverCabestrante.IsActive);
            bool activoGarra = teclaGarra || (hoverGarra != null && hoverGarra.IsActive);
            bool activoPasivo = hoverPasivo != null && hoverPasivo.IsActive;

            if (hoverPluma != null) hoverPluma.SetForcedHighlight(activoPluma);
            if (hoverCarro != null) hoverCarro.SetForcedHighlight(activoCarro);
            if (hoverCabestrante != null) hoverCabestrante.SetForcedHighlight(activoCab);
            if (hoverGarra != null) hoverGarra.SetForcedHighlight(activoGarra);

            AplicarBrilloNodo(controller.brazoPluma, new Color(0.92f, 0.72f, 0.12f), activoPluma, incluirHijosArticulados: false);
            AplicarBrilloNodo(controller.carroRiel, new Color(0.90f, 0.42f, 0.10f), activoCarro, incluirHijosArticulados: false);
            AplicarBrilloNodo(controller.ejeCabestrante, new Color(0.18f, 0.80f, 0.44f), activoCab, incluirHijosArticulados: false);
            AplicarBrilloNodo(controller.cabezalPinzaPasivo, new Color(0.15f, 0.68f, 0.88f), activoPasivo, incluirHijosArticulados: false);
            AplicarBrilloNodo(controller.dedoIzq, new Color(0.85f, 0.22f, 0.18f), activoGarra, incluirHijosArticulados: false);
            AplicarBrilloNodo(controller.dedoDer, new Color(0.85f, 0.22f, 0.18f), activoGarra, incluirHijosArticulados: false);
        }

        private void AplicarBrilloNodo(ArticulationBody nodo, Color baseColor, bool resaltado, bool incluirHijosArticulados)
        {
            if (nodo == null)
            {
                return;
            }

            float pulso = resaltado ? (0.32f + 0.12f * Mathf.Sin(Time.unscaledTime * 8f)) : 0f;
            Color finalColor = resaltado ? Color.Lerp(baseColor, Color.white, pulso) : baseColor;

            foreach (Transform child in nodo.transform)
            {
                if (!incluirHijosArticulados && child.GetComponent<ArticulationBody>() != null)
                {
                    continue;
                }

                // No alterar el color del contrapeso oscuro de la torre en BrazoPluma
                if (child.name.Contains("Contrapeso"))
                {
                    continue;
                }

                MeshRenderer mr = child.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.GetPropertyBlock(_mpb);
                    _mpb.SetColor(BaseColorId, finalColor);
                    _mpb.SetColor(ColorId, finalColor);
                    mr.SetPropertyBlock(_mpb);
                }
            }
        }
    }
}
