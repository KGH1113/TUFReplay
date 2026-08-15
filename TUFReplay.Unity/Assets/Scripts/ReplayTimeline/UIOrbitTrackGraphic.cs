using UnityEngine;
using UnityEngine.UI;

namespace TUFReplay.Unity.ReplayTimeline
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/TUFReplay/Orbit Track Graphic")]
    public sealed class UIOrbitTrackGraphic : MaskableGraphic
    {
        private static readonly float[] DefaultMarkers = { 0.12f, 0.27f, 0.43f, 0.61f, 0.79f, 0.92f };

        [SerializeField, Range(16, 128)] private int segments = 72;
        [SerializeField, Range(0f, 1f)] private float progress = 84f / 228f;
        [SerializeField, Min(0.5f)] private float trackThickness = 6f;
        [SerializeField, Min(0.5f)] private float progressThickness = 9f;
        [SerializeField] private float curveHeight = 96f;
        [SerializeField] private Color trackColor = new Color(0.48f, 0.52f, 0.6f, 0.22f);
        [SerializeField] private Color progressRed = new Color(1f, 0.19f, 0.25f, 0.82f);
        [SerializeField] private Color progressBlue = new Color(0.12f, 0.42f, 1f, 0.82f);
        [SerializeField] private Color markerColor = new Color(1f, 0.97f, 0.9f, 0.6f);

        public float Progress
        {
            get => progress;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(progress, clamped))
                    return;
                progress = clamped;
                SetVerticesDirty();
            }
        }

        public void ConfigureCurve(float height)
        {
            curveHeight = Mathf.Max(0f, height);
            SetVerticesDirty();
        }

        public Vector2 GetLocalPoint(float normalized)
        {
            Rect rect = rectTransform.rect;
            float t = Mathf.Clamp01(normalized);
            float x = Mathf.Lerp(rect.xMin, rect.xMax, t);
            float halfChord = rect.width * 0.5f;
            float sagitta = Mathf.Clamp(curveHeight, 0.001f, halfChord);
            float radius = (halfChord * halfChord + sagitta * sagitta) / (2f * sagitta);
            float centerY = rect.yMin + sagitta - radius;
            float centeredX = x - rect.center.x;
            float y = centerY + Mathf.Sqrt(Mathf.Max(0f, radius * radius - centeredX * centeredX));
            return new Vector2(x, y);
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            for (int i = 0; i < segments; i++)
            {
                float t0 = i / (float)segments;
                float t1 = (i + 1) / (float)segments;
                AddLine(vertexHelper, GetLocalPoint(t0), GetLocalPoint(t1), trackThickness, trackColor, trackColor);

                if (t0 >= progress)
                    continue;

                float clippedT1 = Mathf.Min(t1, progress);
                Color c0 = Color.Lerp(progressRed, progressBlue, Mathf.Clamp01(t0 / Mathf.Max(progress, 0.001f)));
                Color c1 = Color.Lerp(progressRed, progressBlue, Mathf.Clamp01(clippedT1 / Mathf.Max(progress, 0.001f)));
                AddLine(vertexHelper, GetLocalPoint(t0), GetLocalPoint(clippedT1), progressThickness, c0, c1);
            }

            for (int i = 0; i < DefaultMarkers.Length; i++)
                AddMarker(vertexHelper, GetLocalPoint(DefaultMarkers[i]), 5.5f, markerColor);
        }

        private static void AddLine(VertexHelper vh, Vector2 start, Vector2 end, float width, Color32 startColor, Color32 endColor)
        {
            Vector2 delta = end - start;
            if (delta.sqrMagnitude < 0.0001f)
                return;

            Vector2 direction = delta.normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * (width * 0.5f);
            int index = vh.currentVertCount;
            vh.AddVert(start - normal, startColor, Vector2.zero);
            vh.AddVert(start + normal, startColor, Vector2.up);
            vh.AddVert(end + normal, endColor, Vector2.one);
            vh.AddVert(end - normal, endColor, Vector2.right);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }

        private static void AddMarker(VertexHelper vh, Vector2 center, float size, Color32 markerColor)
        {
            float half = size * 0.5f;
            int index = vh.currentVertCount;
            vh.AddVert(center + new Vector2(0f, -half), markerColor, new Vector2(0.5f, 0f));
            vh.AddVert(center + new Vector2(-half, 0f), markerColor, new Vector2(0f, 0.5f));
            vh.AddVert(center + new Vector2(0f, half), markerColor, new Vector2(0.5f, 1f));
            vh.AddVert(center + new Vector2(half, 0f), markerColor, new Vector2(1f, 0.5f));
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
