using UnityEngine;

namespace FisicaAvanzada.Mechanics
{
    /// <summary>
    /// Cámara orbital de seguimiento para la Grúa de Batería de Gravedad.
    /// Acompaña el giro de la pluma mantendo toda la grúa en cuadro (entre los paneles HMI izquierdo y derecho)
    /// y sigue la posición de la pinza usando EjeCabestrante para excluir la oscilación pendular pasiva del CabezalPinza.
    /// </summary>
    [ExecuteAlways]
    public class GravityBatteryCameraController : MonoBehaviour
    {
        [Header("Referencias del Árbol Cinemático")]
        [Tooltip("Raíz fija de la torre (0, 0, 0).")]
        public Transform baseTorre;

        [Tooltip("Brazo giratorio de la pluma (RevoluteJoint Y).")]
        public Transform brazoPluma;

        [Tooltip("Carro riel que se traslada radialmente por la pluma.")]
        public Transform carroRiel;

        [Tooltip("Eje del cabestrante (PrismaticJoint vertical). Se usa como referencia de la pinza para EXCLUIR la oscilación del SphericalJoint pasivo.")]
        public Transform ejeCabestrante;

        [Header("Posición Orbital (Relativa al Giro de Pluma)")]
        [Tooltip("Distancia horizontal desde el eje de la torre hasta la cámara.")]
        [Range(6f, 22f)]
        public float distanciaOrbital = 12.2f;

        [Tooltip("Ángulo lateral respecto a la dirección de la pluma (ej. 58° la ubica en diagonal lateral para ver torre, pluma y carga en perfil 3/4).")]
        [Range(-170f, 170f)]
        public float anguloLateralGrados = 58f;

        [Tooltip("Altura base de la cámara sobre el suelo.")]
        [Range(1f, 15f)]
        public float alturaBase = 6.2f;

        [Tooltip("Cuánto acompaña la altura de la cámara al descenso/ascenso de la pinza (0 = altura fija, 1 = baja 1:1 con la pinza).")]
        [Range(0f, 1f)]
        public float factorSeguimientoAlturaPinza = 0.25f;

        [Tooltip("Cuánto se desplaza la cámara radialmente con el carro riel (0 = radio orbital fijo respecto a la torre).")]
        [Range(0f, 1f)]
        public float factorAcompanamientoRadialCarro = 0.20f;

        [Header("Encuadre / Punto de Mirada (Sin Oscilación Pasiva)")]
        [Tooltip("Offset vertical desde el origen de EjeCabestrante hasta el centro geométrico de la pinza/mordazas (sin depender del CabezalPinza oscilante).")]
        public float offsetVerticalPinzaSinOscilar = -1.75f;

        [Tooltip("Peso de seguimiento hacia la pinza vs. el centro estructural de la grúa (0 = mira fijo a la grúa, 1 = centra 100% en la pinza). Un valor intermedio (ej. 0.55) sigue a la pinza manteniendo toda la torre y pluma siempre en cuadro.")]
        [Range(0f, 1f)]
        public float pesoMiradaEnPinza = 0.58f;

        [Tooltip("Punto de anclaje estructural en el espacio local de la pluma (X, Y altura media de torre, Z adelanto hacia la pluma) para garantizar que la grúa entera entre en pantalla.")]
        public Vector3 anclaEncuadreGruaLocalPluma = new Vector3(0f, 3.6f, 1.8f);

        [Tooltip("Ajuste fino adicional en pantalla/espacio local de mirada (X = horizontal, Y = vertical).")]
        public Vector2 offsetEncuadre = new Vector2(0f, 0.15f);

        [Header("Suavizado de Movimiento")]
        [Tooltip("Tiempo de suavizado (SmoothDamp) para la traslación orbital de la cámara.")]
        [Range(0.01f, 1.5f)]
        public float tiempoSuavizadoPosicion = 0.22f;

        [Tooltip("Tiempo de suavizado (SmoothDamp) para el punto de mirada de la cámara.")]
        [Range(0.01f, 1.5f)]
        public float tiempoSuavizadoMirada = 0.14f;

        private Vector3 _velocidadPosicion;
        private Vector3 _velocidadMirada;
        private Vector3 _puntoMiradaActual;
        private bool _inicializado;

        private void OnEnable()
        {
            AutoAsignarReferenciasSiFaltan();
            SnapToTarget();
        }

        private void Start()
        {
            AutoAsignarReferenciasSiFaltan();
            if (Application.isPlaying)
            {
                SnapToTarget();
            }
        }

        private void LateUpdate()
        {
            if (brazoPluma == null || ejeCabestrante == null)
            {
                AutoAsignarReferenciasSiFaltan();
                if (brazoPluma == null || ejeCabestrante == null)
                {
                    return;
                }
            }

            CalcularObjetivoCamara(out Vector3 posicionObjetivo, out Vector3 miradaObjetivo);

            if (!Application.isPlaying || !_inicializado)
            {
                transform.position = posicionObjetivo;
                _puntoMiradaActual = miradaObjetivo;
                transform.LookAt(_puntoMiradaActual, Vector3.up);
                _inicializado = true;
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            transform.position = Vector3.SmoothDamp(
                transform.position,
                posicionObjetivo,
                ref _velocidadPosicion,
                tiempoSuavizadoPosicion,
                Mathf.Infinity,
                dt);

            _puntoMiradaActual = Vector3.SmoothDamp(
                _puntoMiradaActual,
                miradaObjetivo,
                ref _velocidadMirada,
                tiempoSuavizadoMirada,
                Mathf.Infinity,
                dt);

            Vector3 dir = _puntoMiradaActual - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }

        /// <summary>
        /// Posiciona instantáneamente la cámara en su objetivo sin suavizado (útil al construir la escena o reiniciar).
        /// </summary>
        [ContextMenu("Snap Camera To Target")]
        public void SnapToTarget()
        {
            AutoAsignarReferenciasSiFaltan();
            if (brazoPluma == null || ejeCabestrante == null)
            {
                return;
            }

            CalcularObjetivoCamara(out Vector3 posicionObjetivo, out Vector3 miradaObjetivo);
            _velocidadPosicion = Vector3.zero;
            _velocidadMirada = Vector3.zero;
            _puntoMiradaActual = miradaObjetivo;
            transform.position = posicionObjetivo;
            transform.LookAt(_puntoMiradaActual, Vector3.up);
            _inicializado = true;
        }

        /// <summary>
        /// Calcula la posición virtual de la pinza usando únicamente EjeCabestrante (PrismaticJoint vertical),
        /// ignorando por completo la rotación pendular del SphericalJoint pasivo (CabezalPinza).
        /// </summary>
        public Vector3 ObtenerPosicionPinzaSinOscilacion()
        {
            if (ejeCabestrante == null)
            {
                return Vector3.zero;
            }

            // EjeCabestrante solo traslada verticalmente y rota con la pluma, nunca oscila con el péndulo pasivo.
            return ejeCabestrante.position + Vector3.up * offsetVerticalPinzaSinOscilar;
        }

        private void CalcularObjetivoCamara(out Vector3 posicionObjetivo, out Vector3 miradaObjetivo)
        {
            Vector3 origenTorre = baseTorre != null ? baseTorre.position : Vector3.zero;

            // Dirección horizontal de la pluma proyectada en el plano XZ
            Vector3 forwardPluma = Vector3.ProjectOnPlane(brazoPluma.forward, Vector3.up);
            if (forwardPluma.sqrMagnitude < 0.0001f)
            {
                forwardPluma = Vector3.forward;
            }
            forwardPluma.Normalize();

            Vector3 rightPluma = Vector3.Cross(Vector3.up, forwardPluma).normalized;

            // Posición de la pinza libre de la oscilación natural del SphericalJoint
            Vector3 posPinzaEstable = ObtenerPosicionPinzaSinOscilacion();

            // Punto estructural de la grúa (en el plano de la pluma) para asegurar que toda la estructura se mantenga visible
            Vector3 posAnclaGrua = origenTorre
                + rightPluma * anclaEncuadreGruaLocalPluma.x
                + Vector3.up * anclaEncuadreGruaLocalPluma.y
                + forwardPluma * anclaEncuadreGruaLocalPluma.z;

            // Punto de mirada combinando el ancla estructural de la grúa y la posición sin oscilar de la pinza
            miradaObjetivo = Vector3.Lerp(posAnclaGrua, posPinzaEstable, pesoMiradaEnPinza);
            miradaObjetivo += rightPluma * offsetEncuadre.x + Vector3.up * offsetEncuadre.y;

            // Acompañamiento radial suave según la extensión del carro
            float distanciaRadialCarro = 2.0f;
            if (carroRiel != null)
            {
                Vector3 deltaCarro = carroRiel.position - origenTorre;
                distanciaRadialCarro = Vector3.Dot(deltaCarro, forwardPluma);
            }

            Vector3 centroOrbitaXZ = origenTorre + forwardPluma * (distanciaRadialCarro * factorAcompanamientoRadialCarro);

            // Dirección orbital rotada 'anguloLateralGrados' respecto al frente de la pluma
            Quaternion rotacionOrbital = Quaternion.AngleAxis(anguloLateralGrados, Vector3.up);
            Vector3 dirOrbitaXZ = rotacionOrbital * forwardPluma;

            // Altura de cámara que acompaña sutilmente la altura de la pinza sin oscilar
            float diferenciaAlturaPinza = posPinzaEstable.y - 3.8f;
            float alturaCamara = Mathf.Max(1.8f, alturaBase + diferenciaAlturaPinza * factorSeguimientoAlturaPinza);

            posicionObjetivo = new Vector3(
                centroOrbitaXZ.x + dirOrbitaXZ.x * distanciaOrbital,
                origenTorre.y + alturaCamara,
                centroOrbitaXZ.z + dirOrbitaXZ.z * distanciaOrbital);
        }

        public void AutoAsignarReferenciasSiFaltan()
        {
            if (baseTorre == null)
            {
                GameObject rootGO = GameObject.Find("GruaBateriaGravedad");
                if (rootGO != null)
                {
                    baseTorre = rootGO.transform;
                }
            }

            if (baseTorre != null)
            {
                if (brazoPluma == null)
                {
                    brazoPluma = baseTorre.Find("BrazoPluma");
                }

                if (brazoPluma != null && carroRiel == null)
                {
                    carroRiel = brazoPluma.Find("CarroRiel");
                }

                if (carroRiel != null && ejeCabestrante == null)
                {
                    ejeCabestrante = carroRiel.Find("EjeCabestrante");
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (brazoPluma == null || ejeCabestrante == null)
            {
                return;
            }

            Vector3 pinzaSinOscilar = ObtenerPosicionPinzaSinOscilacion();
            CalcularObjetivoCamara(out _, out Vector3 miradaObjetivo);

            Gizmos.color = new Color(0.18f, 0.80f, 0.44f, 0.9f);
            Gizmos.DrawWireSphere(pinzaSinOscilar, 0.22f);

            Gizmos.color = new Color(0.95f, 0.80f, 0.20f, 0.9f);
            Gizmos.DrawSphere(miradaObjetivo, 0.14f);
            Gizmos.DrawLine(transform.position, miradaObjetivo);
            Gizmos.DrawLine(miradaObjetivo, pinzaSinOscilar);
        }
    }
}
