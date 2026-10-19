using FisicaAvanzada.Mechanics;
using UnityEngine;
using UnityEngine.UI;

namespace FisicaAvanzada.UI
{
    /// <summary>
    /// Interfaz de control y lectura de estado (Target vs Valor Real por joint) en pantalla.
    /// Cumple el requisito de cátedra: 4 Sliders, 2 Botones y telemetría de estado real de cada joint.
    /// </summary>
    public class GravityBatteryTelemetryUI : MonoBehaviour
    {
        public GravityBatteryController controller;

        [Header("Sliders de Entrada")]
        public Slider sliderPluma;
        public Slider sliderCarro;
        public Slider sliderCabestrante;
        public Slider sliderGarra;

        [Header("Botones de Maniobra")]
        public Button btnCerrarGarra;
        public Button btnAbrirGarra;
        public Button btnPosicionCarga;

        [Header("Textos de Lectura de Estado (Target vs Real)")]
        public Text txtEstadoPluma;
        public Text txtEstadoCarro;
        public Text txtEstadoCabestrante;
        public Text txtEstadoDedoIzq;
        public Text txtEstadoDedoDer;
        public Text txtEstadoCabezalPasivo;

        private bool _sincronizandoUI;

        private void Start()
        {
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

            // Sincronizar posición de los Sliders si el usuario mueve con el teclado
            _sincronizandoUI = true;
            if (sliderPluma != null) sliderPluma.SetValueWithoutNotify(controller.targetPlumaGrados);
            if (sliderCarro != null) sliderCarro.SetValueWithoutNotify(controller.targetCarroMetros);
            if (sliderCabestrante != null) sliderCabestrante.SetValueWithoutNotify(controller.targetCabestranteMetros);
            if (sliderGarra != null) sliderGarra.SetValueWithoutNotify(controller.targetGarraMetros);
            _sincronizandoUI = false;

            ActualizarTelemetria();
        }

        private void ActualizarTelemetria()
        {
            float realPluma = controller.ObtenerPosicionRealGrados(controller.brazoPluma);
            float realCarro = controller.ObtenerPosicionRealMetros(controller.carroRiel);
            float realCabestrante = controller.ObtenerPosicionRealMetros(controller.ejeCabestrante);
            float realDedoIzq = controller.ObtenerPosicionRealMetros(controller.dedoIzq);
            float realDedoDer = controller.ObtenerPosicionRealMetros(controller.dedoDer);
            Vector2 inclinacionPasiva = controller.ObtenerInclinacionPasivaGrados();

            if (txtEstadoPluma != null)
            {
                txtEstadoPluma.text = $"1. BrazoPluma (Revolute Y)    | Target: {controller.targetPlumaGrados,6:F1}°  | Real: {realPluma,6:F1}°";
            }

            if (txtEstadoCarro != null)
            {
                txtEstadoCarro.text = $"2. CarroRiel (Prismatic Z)    | Target: {controller.targetCarroMetros,6:F2} m | Real: {realCarro,6:F2} m";
            }

            if (txtEstadoCabestrante != null)
            {
                txtEstadoCabestrante.text = $"3. Cabestrante (Prismatic -Y) | Target: {controller.targetCabestranteMetros,6:F2} m | Real: {realCabestrante,6:F2} m";
            }

            if (txtEstadoDedoIzq != null)
            {
                txtEstadoDedoIzq.text = $"4. DedoGarra_Izq (Prism +X)   | Target: {controller.targetGarraMetros,6:F2} m | Real: {realDedoIzq,6:F2} m";
            }

            if (txtEstadoDedoDer != null)
            {
                txtEstadoDedoDer.text = $"5. DedoGarra_Der (Prism -X)   | Target: {controller.targetGarraMetros,6:F2} m | Real: {realDedoDer,6:F2} m";
            }

            if (txtEstadoCabezalPasivo != null)
            {
                txtEstadoCabezalPasivo.text = $"6. CabezalPinza (Spherical PASIVO) | Oscilación Real: ({inclinacionPasiva.x,5:F1}°, {inclinacionPasiva.y,5:F1}°)";
            }
        }
    }
}
