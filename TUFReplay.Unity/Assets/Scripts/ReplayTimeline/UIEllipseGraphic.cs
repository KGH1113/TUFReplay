using UnityEngine;
using UnityEngine.UI;

namespace TUFReplay.Unity.ReplayTimeline
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/TUFReplay/Ellipse Graphic")]
    public sealed class UIEllipseGraphic : MaskableGraphic
    {
        [SerializeField, Range(12, 96)] private int segments = 48;
        [SerializeField, Min(0.5f)] private float thickness = 1f;

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = GetPixelAdjustedRect();
            Vector2 radius = rect.size * 0.5f;
            Vector2 center = rect.center;

            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.PI * 2f * i / segments;
                float a1 = Mathf.PI * 2f * (i + 1) / segments;
                AddLine(vertexHelper,
                    center + new Vector2(Mathf.Cos(a0) * radius.x, Mathf.Sin(a0) * radius.y),
                    center + new Vector2(Mathf.Cos(a1) * radius.x, Mathf.Sin(a1) * radius.y),
                    thickness,
                    color);
            }
        }

        private static void AddLine(VertexHelper vertexHelper, Vector2 start, Vector2 end, float width, Color32 lineColor)
        {
            Vector2 direction = (end - start).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * (width * 0.5f);
            int index = vertexHelper.currentVertCount;
            vertexHelper.AddVert(start - normal, lineColor, Vector2.zero);
            vertexHelper.AddVert(start + normal, lineColor, Vector2.up);
            vertexHelper.AddVert(end + normal, lineColor, Vector2.one);
            vertexHelper.AddVert(end - normal, lineColor, Vector2.right);
            vertexHelper.AddTriangle(index, index + 1, index + 2);
            vertexHelper.AddTriangle(index, index + 2, index + 3);
        }
    }
}
