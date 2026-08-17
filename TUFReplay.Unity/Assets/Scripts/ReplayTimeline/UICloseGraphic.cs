using UnityEngine;
using UnityEngine.UI;

namespace TUFReplay.Unity.ReplayTimeline
{
  [RequireComponent(typeof(CanvasRenderer))]
  [AddComponentMenu("UI/TUFReplay/Close Graphic")]
  public sealed class UICloseGraphic : MaskableGraphic
  {
    [SerializeField, Min(0.5f)]
    private float strokeWidth = 2f;

    [SerializeField, Min(1f)]
    private float extent = 4f;

    public void Configure(Color closeColor, float width = 2f, float iconExtent = 4f)
    {
      color = closeColor;
      strokeWidth = Mathf.Max(0.5f, width);
      extent = Mathf.Max(1f, iconExtent);
      raycastTarget = false;
      SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
      vertexHelper.Clear();
      Rect rect = GetPixelAdjustedRect();
      if (rect.width <= 0f || rect.height <= 0f)
        return;

      Vector2 center = new Vector2(Mathf.Floor(rect.center.x) + 0.5f, Mathf.Floor(rect.center.y) + 0.5f);
      float resolvedExtent = Mathf.Min(extent, Mathf.Min(rect.width, rect.height) * 0.36f);
      AddRoundedStroke(
        vertexHelper,
        center + new Vector2(-resolvedExtent, -resolvedExtent),
        center + new Vector2(resolvedExtent, resolvedExtent)
      );
      AddRoundedStroke(
        vertexHelper,
        center + new Vector2(-resolvedExtent, resolvedExtent),
        center + new Vector2(resolvedExtent, -resolvedExtent)
      );
    }

    private void AddRoundedStroke(VertexHelper vertexHelper, Vector2 start, Vector2 end)
    {
      Vector2 segment = end - start;
      if (segment.sqrMagnitude <= Mathf.Epsilon)
        return;

      float radius = strokeWidth * 0.5f;
      Vector2 normal = new Vector2(-segment.y, segment.x).normalized * radius;
      int vertexStart = vertexHelper.currentVertCount;
      vertexHelper.AddVert(start + normal, color, Vector2.zero);
      vertexHelper.AddVert(start - normal, color, Vector2.zero);
      vertexHelper.AddVert(end - normal, color, Vector2.zero);
      vertexHelper.AddVert(end + normal, color, Vector2.zero);
      vertexHelper.AddTriangle(vertexStart, vertexStart + 1, vertexStart + 2);
      vertexHelper.AddTriangle(vertexStart, vertexStart + 2, vertexStart + 3);
      AddRoundCap(vertexHelper, start, radius);
      AddRoundCap(vertexHelper, end, radius);
    }

    private void AddRoundCap(VertexHelper vertexHelper, Vector2 center, float radius)
    {
      const int Segments = 8;
      int centerVertex = vertexHelper.currentVertCount;
      vertexHelper.AddVert(center, color, Vector2.zero);
      for (int index = 0; index <= Segments; index++)
      {
        float angle = index * Mathf.PI * 2f / Segments;
        vertexHelper.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
      }

      for (int index = 0; index < Segments; index++)
        vertexHelper.AddTriangle(centerVertex, centerVertex + index + 1, centerVertex + index + 2);
    }
  }
}
