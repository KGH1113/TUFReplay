using UnityEngine;
using UnityEngine.UI;

namespace TUFReplay.Unity.ReplayTimeline
{
  [RequireComponent(typeof(CanvasRenderer))]
  [AddComponentMenu("UI/TUFReplay/Close Graphic")]
  public sealed class UICloseGraphic : MaskableGraphic
  {
    public void Configure(Color closeColor)
    {
      color = closeColor;
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
      AddStroke(vertexHelper, center + new Vector2(-4f, -4f), center + new Vector2(4f, 4f), 2f);
      AddStroke(vertexHelper, center + new Vector2(-4f, 4f), center + new Vector2(4f, -4f), 2f);
    }

    private void AddStroke(VertexHelper vertexHelper, Vector2 start, Vector2 end, float width)
    {
      Vector2 segment = end - start;
      if (segment.sqrMagnitude <= Mathf.Epsilon)
        return;

      Vector2 normal = new Vector2(-segment.y, segment.x).normalized * (width * 0.5f);
      int vertexStart = vertexHelper.currentVertCount;
      vertexHelper.AddVert(start + normal, color, Vector2.zero);
      vertexHelper.AddVert(start - normal, color, Vector2.zero);
      vertexHelper.AddVert(end - normal, color, Vector2.zero);
      vertexHelper.AddVert(end + normal, color, Vector2.zero);
      vertexHelper.AddTriangle(vertexStart, vertexStart + 1, vertexStart + 2);
      vertexHelper.AddTriangle(vertexStart, vertexStart + 2, vertexStart + 3);
    }
  }
}
