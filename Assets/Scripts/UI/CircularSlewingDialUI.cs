using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FisicaAvanzada.UI
{
    /// <summary>
    /// Control interactivo circular (tipo radar/brújula de grúa torre) para el giro de la pluma (-180° a +180°).
    /// Permite arrastrar en círculo con el mouse y muestra simultáneamente el ángulo Target y el ángulo Real físico.
    /// </summary>
    public class CircularSlewingDialUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Referencias Visuales del Dial")]
        public RectTransform dialRect;
        public RectTransform agujaTarget;
        public RectTransform agujaReal;
        public Text txtAnguloCentral;

        [Header("Estado")]
        public bool interactable = true;
        public bool IsHovered { get; private set; }
        public bool IsDragging { get; private set; }

        public event Action<float> OnAngleChanged;

        private void Awake()
        {
            if (dialRect == null)
            {
                dialRect = GetComponent<RectTransform>();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            IsHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            IsHovered = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!interactable)
            {
                return;
            }
            IsDragging = true;
            ActualizarAnguloDesdePunzon(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!interactable)
            {
                return;
            }
            ActualizarAnguloDesdePunzon(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsDragging = false;
        }

        private void ActualizarAnguloDesdePunzon(PointerEventData eventData)
        {
            if (dialRect == null)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dialRect, eventData.position, eventData.pressEventCamera, out Vector2 localPos))
            {
                // En el dial vista superior:
                // Arriba (+Y en UI) corresponde a 0° (+Z en mundo, apuntando a la Zona de Carga).
                // Derecha (+X en UI) corresponde a +90° (giro horario en eje Y hacia +X).
                // Izquierda (-X en UI) corresponde a -90° (apuntando hacia la Plataforma Alta en -X).
                float anguloGrados = Mathf.Atan2(localPos.x, localPos.y) * Mathf.Rad2Deg;
                anguloGrados = Mathf.Clamp(anguloGrados, -180f, 180f);
                OnAngleChanged?.Invoke(anguloGrados);
            }
        }

        /// <summary>
        /// Actualiza la orientación visual de las agujas Target y Real en el radar.
        /// </summary>
        public void ActualizarVisuales(float targetGrados, float realGrados)
        {
            // En UI 2D, rotación Z negativa gira en sentido horario (donde +X es +90° y -X es -90°)
            if (agujaTarget != null)
            {
                agujaTarget.localRotation = Quaternion.Euler(0f, 0f, -targetGrados);
            }

            if (agujaReal != null)
            {
                agujaReal.localRotation = Quaternion.Euler(0f, 0f, -realGrados);
            }

            if (txtAnguloCentral != null)
            {
                txtAnguloCentral.text = $"{realGrados:F0}°";
            }
        }
    }
}
