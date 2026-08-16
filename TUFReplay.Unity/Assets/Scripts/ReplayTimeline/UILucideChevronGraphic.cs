using UnityEngine;
using UnityEngine.UI;

namespace TUFReplay.Unity.ReplayTimeline
{
  [RequireComponent(typeof(CanvasRenderer))]
  [AddComponentMenu("UI/TUFReplay/Lucide Chevron Graphic")]
  public sealed class UILucideChevronGraphic : MaskableGraphic
  {
    [SerializeField]
    private float strokeWidth = 2f;

    public void Configure(Color strokeColor, float width = 2f)
    {
      color = strokeColor;
      strokeWidth = Mathf.Max(0.5f, width);
      raycastTarget = false;
      SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
      vertexHelper.Clear();
      Rect rect = GetPixelAdjustedRect();
      if (rect.width <= 0f || rect.height <= 0f)
        return;

      float scale = Mathf.Min(rect.width, rect.height) / 24f;
      Vector2 origin = rect.center - new Vector2(12f, 12f) * scale;
      // SVG's Y axis points down, while Unity UI's local Y axis points up.
      Vector2 start = origin + new Vector2(6f, 15f) * scale;
      Vector2 join = origin + new Vector2(12f, 9f) * scale;
      Vector2 end = origin + new Vector2(18f, 15f) * scale;
      float radius = strokeWidth * scale * 0.5f;

      AddStroke(vertexHelper, start, join, radius);
      AddStroke(vertexHelper, join, end, radius);
      AddRoundCap(vertexHelper, start, radius);
      AddRoundCap(vertexHelper, join, radius);
      AddRoundCap(vertexHelper, end, radius);
    }

    private void AddStroke(VertexHelper vertexHelper, Vector2 start, Vector2 end, float radius)
    {
      Vector2 direction = (end - start).normalized;
      Vector2 normal = new Vector2(-direction.y, direction.x) * radius;
      int firstVertex = vertexHelper.currentVertCount;
      AddVertex(vertexHelper, start - normal);
      AddVertex(vertexHelper, start + normal);
      AddVertex(vertexHelper, end + normal);
      AddVertex(vertexHelper, end - normal);
      vertexHelper.AddTriangle(firstVertex, firstVertex + 1, firstVertex + 2);
      vertexHelper.AddTriangle(firstVertex, firstVertex + 2, firstVertex + 3);
    }

    private void AddRoundCap(VertexHelper vertexHelper, Vector2 center, float radius)
    {
      const int segments = 8;
      int centerVertex = vertexHelper.currentVertCount;
      AddVertex(vertexHelper, center);
      for (int index = 0; index <= segments; index++)
      {
        float angle = index * Mathf.PI * 2f / segments;
        AddVertex(vertexHelper, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
      }

      for (int index = 0; index < segments; index++)
        vertexHelper.AddTriangle(centerVertex, centerVertex + index + 1, centerVertex + index + 2);
    }

    private void AddVertex(VertexHelper vertexHelper, Vector2 position)
    {
      vertexHelper.AddVert(position, color, Vector2.zero);
    }
  }
}
