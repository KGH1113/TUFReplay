using UnityEngine;
using UnityEngine.UI;

namespace TUFReplay.Unity.ReplayTimeline
{
  [RequireComponent(typeof(CanvasRenderer))]
  [AddComponentMenu("UI/TUFReplay/Edge Arrow Graphic")]
  public sealed class UIEdgeArrowGraphic : MaskableGraphic
  {
    [SerializeField]
    private bool pointsLeft;

    public void Configure(Color arrowColor, bool left)
    {
      color = arrowColor;
      pointsLeft = left;
      raycastTarget = false;
      SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
      vertexHelper.Clear();
      Rect rect = GetPixelAdjustedRect();
      if (rect.width <= 0f || rect.height <= 0f)
        return;

      float direction = pointsLeft ? -1f : 1f;
      Vector2 center = new Vector2(Mathf.Floor(rect.center.x) + 0.5f, Mathf.Floor(rect.center.y) + 0.5f);
      Vector2 upperTail = center + new Vector2(-direction * 5f, 8f);
      Vector2 tip = center + new Vector2(direction * 6f, 0f);
      Vector2 lowerTail = center + new Vector2(-direction * 5f, -8f);
      AddChevron(vertexHelper, upperTail, tip, lowerTail, 3f);
    }

    private void AddChevron(VertexHelper vertexHelper, Vector2 start, Vector2 join, Vector2 end, float width)
    {
      Vector2 firstDirection = join - start;
      Vector2 secondDirection = end - join;
      if (firstDirection.sqrMagnitude <= Mathf.Epsilon || secondDirection.sqrMagnitude <= Mathf.Epsilon)
        return;

      firstDirection.Normalize();
      secondDirection.Normalize();
      Vector2 firstNormal = new Vector2(-firstDirection.y, firstDirection.x);
      Vector2 secondNormal = new Vector2(-secondDirection.y, secondDirection.x);
      Vector2 miter = (firstNormal + secondNormal).normalized;
      float halfWidth = width * 0.5f;
      float miterDenominator = Vector2.Dot(miter, firstNormal);
      if (Mathf.Abs(miterDenominator) <= Mathf.Epsilon)
        return;

      Vector2 startOffset = firstNormal * halfWidth;
      Vector2 joinOffset = miter * (halfWidth / miterDenominator);
      Vector2 endOffset = secondNormal * halfWidth;
      int vertexStart = vertexHelper.currentVertCount;
      vertexHelper.AddVert(start + startOffset, color, Vector2.zero);
      vertexHelper.AddVert(start - startOffset, color, Vector2.zero);
      vertexHelper.AddVert(join + joinOffset, color, Vector2.zero);
      vertexHelper.AddVert(join - joinOffset, color, Vector2.zero);
      vertexHelper.AddVert(end + endOffset, color, Vector2.zero);
      vertexHelper.AddVert(end - endOffset, color, Vector2.zero);
      vertexHelper.AddTriangle(vertexStart, vertexStart + 1, vertexStart + 3);
      vertexHelper.AddTriangle(vertexStart, vertexStart + 3, vertexStart + 2);
      vertexHelper.AddTriangle(vertexStart + 2, vertexStart + 3, vertexStart + 5);
      vertexHelper.AddTriangle(vertexStart + 2, vertexStart + 5, vertexStart + 4);
    }
  }
}
