using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FisicaAvanzada.UI
{
    /// <summary>
    /// Detecta cuando el usuario pasa el cursor o interactúa con una tarjeta de control UI
    /// para resaltar visualmente tanto la tarjeta como la pieza 3D correspondiente en la grúa.
    /// </summary>
    public class UIControlHoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Image cardBorderImage;
        public Color normalBorderColor = new Color(1f, 1f, 1f, 0.12f);
        public Color activeBorderColor = new Color(1f, 0.85f, 0.2f, 0.95f);

        public bool IsHovered { get; private set; }
        public bool IsPressed { get; private set; }
        public bool IsActive => IsHovered || IsPressed;

        public void OnPointerEnter(PointerEventData eventData)
        {
            IsHovered = true;
            ActualizarBorde();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            IsHovered = false;
            ActualizarBorde();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsPressed = true;
            ActualizarBorde();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsPressed = false;
            ActualizarBorde();
        }

        public void SetForcedHighlight(bool forced)
        {
            if (cardBorderImage != null)
            {
                cardBorderImage.color = (IsActive || forced) ? activeBorderColor : normalBorderColor;
            }
        }

        private void ActualizarBorde()
        {
            if (cardBorderImage != null)
            {
                cardBorderImage.color = IsActive ? activeBorderColor : normalBorderColor;
            }
        }
    }
}
