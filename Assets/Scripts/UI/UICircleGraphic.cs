using UnityEngine;
using UnityEngine.UI;

namespace FisicaAvanzada.UI
{
    /// <summary>
    /// Gráfico vectorial ligero para Unity UI (Canvas) que dibuja círculos rellenos, anillos o arcos angulares
    /// sin depender de texturas externas. Ideal para radares de giro, dianas de oscilación y perillas circulares.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public class UICircleGraphic : MaskableGraphic
    {
        [Tooltip("Si es true, dibuja solo el borde (anillo). Si es false, dibuja un círculo/sector relleno.")]
        public bool isRing = false;

        [Tooltip("Grosor del anillo en píxeles cuando isRing = true.")]
        public float ringThickness = 4f;

        [Range(12, 128)]
        public int segments = 64;

        [Tooltip("Ángulo inicial en grados (0° = arriba/+Y, sentido horario).")]
        public float startAngleDeg = -180f;

        [Tooltip("Ángulo final en grados (0° = arriba/+Y, sentido horario).")]
        public float endAngleDeg = 180f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            float outerRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (outerRadius <= 0.1f)
            {
                return;
            }

            Vector2 center = rect.center;
            float innerRadius = isRing ? Mathf.Max(0f, outerRadius - ringThickness) : 0f;

            int segCount = Mathf.Max(8, segments);
            float totalAngle = endAngleDeg - startAngleDeg;
            float stepDeg = totalAngle / segCount;

            UIVertex vert = UIVertex.simpleVert;
            vert.color = color;

            if (!isRing)
            {
                // Centro del círculo/sector (índice 0)
                vert.position = center;
                vh.AddVert(vert);

                for (int i = 0; i <= segCount; i++)
                {
                    float angleDeg = startAngleDeg + i * stepDeg;
                    float rad = angleDeg * Mathf.Deg2Rad;
                    // 0° arriba (+Y), +90° derecha (+X)
                    Vector2 dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
                    vert.position = center + dir * outerRadius;
                    vh.AddVert(vert);
                }

                for (int i = 1; i <= segCount; i++)
                {
                    vh.AddTriangle(0, i, i + 1);
                }
            }
            else
            {
                for (int i = 0; i <= segCount; i++)
                {
                    float angleDeg = startAngleDeg + i * stepDeg;
                    float rad = angleDeg * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));

                    vert.position = center + dir * innerRadius;
                    vh.AddVert(vert);

                    vert.position = center + dir * outerRadius;
                    vh.AddVert(vert);
                }

                for (int i = 0; i < segCount; i++)
                {
                    int idx = i * 2;
                    vh.AddTriangle(idx, idx + 1, idx + 3);
                    vh.AddTriangle(idx, idx + 3, idx + 2);
                }
            }
        }
    }
}
