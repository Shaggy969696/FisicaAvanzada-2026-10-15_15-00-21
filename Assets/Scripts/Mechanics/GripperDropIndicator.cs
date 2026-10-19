using UnityEngine;

namespace FisicaAvanzada.Mechanics
{
    /// <summary>
    /// Proyecta un indicador visual tipo "sombra / diana de descenso" directamente debajo de la pinza
    /// mediante un Raycast vertical hacia abajo (-Y) que ignora los propios colliders de la grúa.
    /// Si la pinza sostiene un bloque entre sus mordazas, también lo ignora para proyectarse sobre la superficie de apoyo real.
    /// </summary>
    [ExecuteAlways]
    public class GripperDropIndicator : MonoBehaviour
    {
        [Header("Referencias de la Grúa")]
        [Tooltip("Raíz de la grúa para filtrar todos sus colliders propios del Raycast.")]
        public Transform raizGrua;

        [Tooltip("Origen del descenso sin oscilación (EjeCabestrante).")]
        public Transform ejeCabestrante;

        [Tooltip("Chasis de la garra para orientar la huella con el giro de la pinza.")]
        public Transform chasisGarra;

        [Header("Elementos Visuales del Indicador (Auto-generados o Asignados)")]
        [Tooltip("Nodo que se posiciona sobre la superficie de impacto a pocos milímetros de altura.")]
        public Transform marcadorSuperficie;

        [Tooltip("Anillo exterior de la huella de aterrizaje.")]
        public Transform anilloExterior;

        [Tooltip("Núcleo central del indicador.")]
        public Transform nucleoCentral;

        [Tooltip("Línea guía vertical desde la pinza hasta la superficie de apoyo.")]
        public LineRenderer lineaGuiaVertical;

        [Header("Configuración de Detección")]
        [Tooltip("Distancia máxima del rayo vertical hacia abajo.")]
        public float distanciaMaximaRayo = 20f;

        [Tooltip("Separación vertical respecto a la superficie detectada para evitar Z-fighting.")]
        public float offsetSobreSuperficie = 0.012f;

        private readonly RaycastHit[] _hitsBuffer = new RaycastHit[16];
        private Collider[] _collidersGrua;

        private void OnEnable()
        {
            InicializarCacheColliders();
            ActualizarIndicador();
        }

        private void Start()
        {
            InicializarCacheColliders();
            ActualizarIndicador();
        }

        public void InicializarCacheColliders()
        {
            if (raizGrua == null)
            {
                GameObject rootGO = GameObject.Find("GruaBateriaGravedad");
                if (rootGO != null)
                {
                    raizGrua = rootGO.transform;
                }
            }

            if (raizGrua != null)
            {
                _collidersGrua = raizGrua.GetComponentsInChildren<Collider>(true);
            }
        }

        private void LateUpdate()
        {
            ActualizarIndicador();
        }

        private void ActualizarIndicador()
        {
            if (ejeCabestrante == null || marcadorSuperficie == null)
            {
                return;
            }

            // Punto de partida en el eje vertical de descenso (debajo del cabestrante)
            Vector3 origenVertical = ejeCabestrante.position + Vector3.down * 1.15f;
            Vector3 origenRayo = new Vector3(origenVertical.x, ejeCabestrante.position.y, origenVertical.z);

            if (!ObtenerSuperficieDebajo(origenRayo, out RaycastHit hitValido))
            {
                // Fallback al nivel del suelo (y = 0) si no hubiera collider debajo
                hitValido.point = new Vector3(origenRayo.x, 0f, origenRayo.z);
                hitValido.normal = Vector3.up;
            }

            Vector3 puntoImpacto = hitValido.point + hitValido.normal * offsetSobreSuperficie;
            marcadorSuperficie.position = puntoImpacto;

            // Alinear el marcador con la normal de la superficie y el giro horizontal de la pluma/garra
            Vector3 forwardRef = chasisGarra != null ? chasisGarra.forward : ejeCabestrante.forward;
            Vector3 forwardProyectado = Vector3.ProjectOnPlane(forwardRef, hitValido.normal);
            if (forwardProyectado.sqrMagnitude < 0.001f)
            {
                forwardProyectado = Vector3.forward;
            }
            marcadorSuperficie.rotation = Quaternion.LookRotation(forwardProyectado.normalized, hitValido.normal);

            // Escala sutil reactiva a la altura restante para dar feedback de proximidad al apoyar
            float alturaRestante = Mathf.Max(0f, origenVertical.y - hitValido.point.y);
            if (anilloExterior != null)
            {
                float factorAltura = Mathf.Clamp01(alturaRestante / 5.0f);
                float diametro = Mathf.Lerp(0.86f, 1.08f, factorAltura);
                anilloExterior.localScale = new Vector3(diametro, 0.008f, diametro);
            }

            // Actualizar línea guía vertical desde la pinza hasta la sombra en el piso/bloque
            if (lineaGuiaVertical != null)
            {
                Vector3 inicioLinea = new Vector3(puntoImpacto.x, Mathf.Max(puntoImpacto.y + 0.02f, origenVertical.y - 0.55f), puntoImpacto.z);
                lineaGuiaVertical.positionCount = 2;
                lineaGuiaVertical.SetPosition(0, inicioLinea);
                lineaGuiaVertical.SetPosition(1, puntoImpacto);
            }
        }

        private bool ObtenerSuperficieDebajo(Vector3 origenRayo, out RaycastHit mejorHit)
        {
            mejorHit = default;
            int hitCount = Physics.RaycastNonAlloc(
                origenRayo,
                Vector3.down,
                _hitsBuffer,
                distanciaMaximaRayo,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            float menorDistancia = float.MaxValue;
            bool encontrado = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hitsBuffer[i];
                if (hit.collider == null)
                {
                    continue;
                }

                // 1. Ignorar cualquier collider que forme parte del árbol de la grúa (ArticulationBody)
                if (EsColliderDeLaGrua(hit.collider))
                {
                    continue;
                }

                // 2. Ignorar todos los bloques de carga móviles (Rigidbody grises) para que el indicador
                // siempre se proyecte en el suelo o plataforma directamente debajo del bloque antes y durante el agarre.
                if (hit.collider.attachedRigidbody != null)
                {
                    continue;
                }

                if (hit.distance < menorDistancia)
                {
                    menorDistancia = hit.distance;
                    mejorHit = hit;
                    encontrado = true;
                }
            }

            return encontrado;
        }

        private bool EsColliderDeLaGrua(Collider col)
        {
            if (raizGrua != null && col.transform.IsChildOf(raizGrua))
            {
                return true;
            }

            if (_collidersGrua != null)
            {
                for (int i = 0; i < _collidersGrua.Length; i++)
                {
                    if (_collidersGrua[i] == col)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
