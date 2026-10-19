using UnityEngine;
using UnityEngine.InputSystem;

namespace FisicaAvanzada.Mechanics
{
    /// <summary>
    /// Controlador principal de la Grúa de Batería de Gravedad.
    /// Aplica los targets de entrada a los xDrive en FixedUpdate y expone la telemetría (Target vs Real) de cada joint.
    /// </summary>
    public class GravityBatteryController : MonoBehaviour
    {
        [Header("Articulation Bodies Controlados (5 Drives)")]
        public ArticulationBody brazoPluma;
        public ArticulationBody carroRiel;
        public ArticulationBody ejeCabestrante;
        public ArticulationBody dedoIzq;
        public ArticulationBody dedoDer;

        [Header("Articulation Body Pasivo (Gravedad / Inercia)")]
        public ArticulationBody cabezalPinzaPasivo;

        [Header("Referencia Visual del Cable / Cabestrante")]
        [Tooltip("Malla visual hija de EjeCabestrante que se estira visualmente entre el carro y el cabezal.")]
        public Transform cableVisualTransform;

        [Header("Modo de Simulación")]
        [Tooltip("Si está desactivado (Fase 2 - Caída Libre), no se sobreescriben los drives en FixedUpdate.")]
        public bool drivesActivos = true;

        [Header("Targets Actuales de Control")]
        [Range(-180f, 180f)] public float targetPlumaGrados = 0f;
        [Range(0f, 4.5f)] public float targetCarroMetros = 2.0f;
        [Range(0f, 4.0f)] public float targetCabestranteMetros = 0.0f;
        [Range(0f, 0.35f)] public float targetGarraMetros = 0.0f;

        [Header("Velocidades de Control por Teclado")]
        public float velocidadGiroPluma = 45f;       // grados/s
        public float velocidadCarro = 1.2f;          // m/s
        public float velocidadCabestrante = 1.2f;    // m/s
        public float velocidadGarra = 0.25f;         // m/s

        private void Update()
        {
            if (!drivesActivos)
            {
                return;
            }

            LeerEntradaTeclado();
        }

        private void LateUpdate()
        {
            ActualizarCableVisual();
        }

        private void ActualizarCableVisual()
        {
            if (ejeCabestrante == null)
            {
                return;
            }

            if (cableVisualTransform == null)
            {
                Transform encontrado = ejeCabestrante.transform.Find("Visual_CableRigido");
                if (encontrado != null)
                {
                    cableVisualTransform = encontrado;
                }
                else
                {
                    return;
                }
            }

            // Extensión real d (en metros) que ha bajado el PrismaticJoint respecto al CarroRiel
            float extensionReal = ObtenerPosicionRealMetros(ejeCabestrante);
            extensionReal = Mathf.Max(0f, extensionReal);

            // En el espacio local de EjeCabestrante:
            // - El CarroRiel queda arriba en y = +extensionReal
            // - El CabezalPinza está abajo en y = -1.20f
            float largoBase = 1.20f;
            float largoTotal = largoBase + extensionReal;
            float centroY = (extensionReal - largoBase) * 0.5f;

            cableVisualTransform.localPosition = new Vector3(0f, centroY, 0f);
            cableVisualTransform.localScale = new Vector3(0.12f, largoTotal * 0.5f, 0.12f);
        }

        private void FixedUpdate()
        {
            if (!drivesActivos)
            {
                return;
            }

            AplicarTargetDrive(brazoPluma, targetPlumaGrados);
            AplicarTargetDrive(carroRiel, targetCarroMetros);
            AplicarTargetDrive(ejeCabestrante, targetCabestranteMetros);
            AplicarTargetDrive(dedoIzq, targetGarraMetros);
            AplicarTargetDrive(dedoDer, targetGarraMetros);
        }

        private void LeerEntradaTeclado()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            float dt = Time.deltaTime;

            // Giro de Pluma: A / D
            if (kb.aKey.isPressed)
                targetPlumaGrados -= velocidadGiroPluma * dt;
            if (kb.dKey.isPressed)
                targetPlumaGrados += velocidadGiroPluma * dt;
            targetPlumaGrados = Mathf.Clamp(targetPlumaGrados, -180f, 180f);

            // Carro Riel (Radial): W / S
            if (kb.wKey.isPressed)
                targetCarroMetros += velocidadCarro * dt;
            if (kb.sKey.isPressed)
                targetCarroMetros -= velocidadCarro * dt;
            targetCarroMetros = Mathf.Clamp(targetCarroMetros, 0f, 4.5f);

            // Cabestrante (Vertical): Q (Subir) / E (Bajar) o Flechas Arriba / Abajo
            if (kb.eKey.isPressed || kb.downArrowKey.isPressed)
                targetCabestranteMetros += velocidadCabestrante * dt;
            if (kb.qKey.isPressed || kb.upArrowKey.isPressed)
                targetCabestranteMetros -= velocidadCabestrante * dt;
            targetCabestranteMetros = Mathf.Clamp(targetCabestranteMetros, 0f, 4.0f);

            // Mordazas (Garra): Z (Cerrar) / X (Abrir)
            if (kb.zKey.isPressed)
                targetGarraMetros += velocidadGarra * dt;
            if (kb.xKey.isPressed)
                targetGarraMetros -= velocidadGarra * dt;
            targetGarraMetros = Mathf.Clamp(targetGarraMetros, 0f, 0.35f);
        }

        private static void AplicarTargetDrive(ArticulationBody body, float targetValue)
        {
            if (body == null)
            {
                return;
            }

            ArticulationDrive drive = body.xDrive;
            drive.target = Mathf.Clamp(targetValue, drive.lowerLimit, drive.upperLimit);
            body.xDrive = drive;
        }

        // =========================================================================
        // Métodos para UI (Sliders y Botones)
        // =========================================================================

        public void SetTargetPluma(float grados)
        {
            targetPlumaGrados = Mathf.Clamp(grados, -180f, 180f);
        }

        public void SetTargetCarro(float metros)
        {
            targetCarroMetros = Mathf.Clamp(metros, 0f, 4.5f);
        }

        public void SetTargetCabestrante(float metros)
        {
            targetCabestranteMetros = Mathf.Clamp(metros, 0f, 4.0f);
        }

        public void SetTargetGarra(float metros)
        {
            targetGarraMetros = Mathf.Clamp(metros, 0f, 0.35f);
        }

        public void CerrarGarraCompleta()
        {
            targetGarraMetros = 0.35f;
        }

        public void AbrirGarraCompleta()
        {
            targetGarraMetros = 0.0f;
        }

        public void IrAPosicionCarga()
        {
            targetPlumaGrados = 0f;
            targetCarroMetros = 2.0f;
            targetCabestranteMetros = 0.0f;
        }

        // =========================================================================
        // Lectura de Estado Real (jointPosition) desde PhysX
        // =========================================================================

        public float ObtenerPosicionRealGrados(ArticulationBody body)
        {
            if (body == null || body.dofCount < 1)
            {
                return 0f;
            }
            return body.jointPosition[0] * Mathf.Rad2Deg;
        }

        public float ObtenerPosicionRealMetros(ArticulationBody body)
        {
            if (body == null || body.dofCount < 1)
            {
                return 0f;
            }
            return body.jointPosition[0];
        }

        public Vector2 ObtenerInclinacionPasivaGrados()
        {
            if (cabezalPinzaPasivo == null || cabezalPinzaPasivo.dofCount < 3)
            {
                return Vector2.zero;
            }
            float swingY = cabezalPinzaPasivo.jointPosition[1] * Mathf.Rad2Deg;
            float swingZ = cabezalPinzaPasivo.jointPosition[2] * Mathf.Rad2Deg;
            return new Vector2(swingY, swingZ);
        }
    }
}
