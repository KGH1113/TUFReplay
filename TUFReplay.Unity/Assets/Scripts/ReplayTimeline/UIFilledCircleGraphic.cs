using UnityEngine;
using UnityEngine.UI;

namespace TUFReplay.Unity.ReplayTimeline
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/TUFReplay/Filled Circle Graphic")]
    public sealed class UIFilledCircleGraphic : MaskableGraphic
    {
        [SerializeField, Range(12, 96)] private int segments = 48;

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            int count = Mathf.Clamp(segments, 12, 96);

            vertexHelper.AddVert(center, color, new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= count; i++)
            {
                float angle = Mathf.PI * 2f * i / count;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertexHelper.AddVert(center + direction * radius, color, direction * 0.5f + Vector2.one * 0.5f);
                if (i > 0)
                    vertexHelper.AddTriangle(0, i, i + 1);
            }
        }
    }
}
