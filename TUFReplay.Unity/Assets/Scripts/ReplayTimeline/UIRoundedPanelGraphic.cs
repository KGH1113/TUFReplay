using UnityEngine;
using UnityEngine.UI;

namespace TUFReplay.Unity.ReplayTimeline
{
  [RequireComponent(typeof(CanvasRenderer))]
  [AddComponentMenu("UI/TUFReplay/Rounded Panel Graphic")]
  public sealed class UIRoundedPanelGraphic : MaskableGraphic
  {
    [SerializeField]
    private Color fillColor = new Color(0.071f, 0.078f, 0.114f, 0.82f);

    [SerializeField]
    private Color borderColor = new Color(0.96f, 0.93f, 0.86f, 0.14f);

    [SerializeField, Min(0f)]
    private float borderWidth = 1.5f;

    [SerializeField, Min(0f)]
    private float cornerRadius = 18f;

    [SerializeField, Range(2, 16)]
    private int cornerSegments = 8;

    [SerializeField]
    private bool roundTopLeft = true;

    [SerializeField]
    private bool roundTopRight = true;

    [SerializeField]
    private bool roundBottomRight = true;

    [SerializeField]
    private bool roundBottomLeft = true;

    public void Configure(Color fill, Color border, float width, float radius)
    {
      fillColor = fill;
      borderColor = border;
      borderWidth = Mathf.Max(0f, width);
      cornerRadius = Mathf.Max(0f, radius);
      SetVerticesDirty();
    }

    public void ConfigureCorners(bool topLeft, bool topRight, bool bottomRight, bool bottomLeft)
    {
      roundTopLeft = topLeft;
      roundTopRight = topRight;
      roundBottomRight = bottomRight;
      roundBottomLeft = bottomLeft;
      SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
      vertexHelper.Clear();
      Rect rect = GetPixelAdjustedRect();
      if (rect.width <= 0f || rect.height <= 0f)
        return;

      float radius = Mathf.Min(cornerRadius, Mathf.Min(rect.width, rect.height) * 0.5f);
      int segmentCount = Mathf.Max(2, cornerSegments);
      int perimeterCount = 4 * (segmentCount + 1);
      Color resolvedFillColor = fillColor * color;
      Color resolvedBorderColor = borderColor * color;

      int fillCenter = vertexHelper.currentVertCount;
      vertexHelper.AddVert(rect.center, resolvedFillColor, Vector2.zero);
      AddPerimeter(vertexHelper, rect, radius, segmentCount, resolvedFillColor);
      for (int index = 0; index < perimeterCount; index++)
      {
        int current = fillCenter + 1 + index;
        int next = fillCenter + 1 + ((index + 1) % perimeterCount);
        vertexHelper.AddTriangle(fillCenter, current, next);
      }

      float width = Mathf.Min(borderWidth, Mathf.Min(rect.width, rect.height) * 0.5f);
      if (width <= 0f || resolvedBorderColor.a <= 0f)
        return;

      Rect innerRect = new Rect(
        rect.xMin + width,
        rect.yMin + width,
        Mathf.Max(0f, rect.width - width * 2f),
        Mathf.Max(0f, rect.height - width * 2f)
      );
      float innerRadius = Mathf.Max(0f, radius - width);
      int borderStart = vertexHelper.currentVertCount;

      for (int corner = 0; corner < 4; corner++)
      {
        float outerCornerRadius = IsCornerRounded(corner) ? radius : 0f;
        float innerCornerRadius = IsCornerRounded(corner) ? innerRadius : 0f;
        for (int segment = 0; segment <= segmentCount; segment++)
        {
          float angle = (-90f + corner * 90f + 90f * segment / segmentCount) * Mathf.Deg2Rad;
          Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
          vertexHelper.AddVert(
            CornerCenter(rect, outerCornerRadius, corner) + direction * outerCornerRadius,
            resolvedBorderColor,
            Vector2.zero
          );
          vertexHelper.AddVert(
            CornerCenter(innerRect, innerCornerRadius, corner) + direction * innerCornerRadius,
            resolvedBorderColor,
            Vector2.zero
          );
        }
      }

      for (int index = 0; index < perimeterCount; index++)
      {
        int next = (index + 1) % perimeterCount;
        int outerCurrent = borderStart + index * 2;
        int innerCurrent = outerCurrent + 1;
        int outerNext = borderStart + next * 2;
        int innerNext = outerNext + 1;
        vertexHelper.AddTriangle(outerCurrent, outerNext, innerNext);
        vertexHelper.AddTriangle(outerCurrent, innerNext, innerCurrent);
      }
    }

    private void AddPerimeter(VertexHelper vertexHelper, Rect rect, float radius, int segmentCount, Color32 color)
    {
      for (int corner = 0; corner < 4; corner++)
      {
        float cornerRadius = IsCornerRounded(corner) ? radius : 0f;
        Vector2 center = CornerCenter(rect, cornerRadius, corner);
        for (int segment = 0; segment <= segmentCount; segment++)
        {
          float angle = (-90f + corner * 90f + 90f * segment / segmentCount) * Mathf.Deg2Rad;
          Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
          vertexHelper.AddVert(center + direction * cornerRadius, color, Vector2.zero);
        }
      }
    }

    private bool IsCornerRounded(int corner)
    {
      switch (corner)
      {
        case 0:
          return roundBottomRight;
        case 1:
          return roundTopRight;
        case 2:
          return roundTopLeft;
        default:
          return roundBottomLeft;
      }
    }

    private static Vector2 CornerCenter(Rect rect, float radius, int corner)
    {
      switch (corner)
      {
        case 0:
          return new Vector2(rect.xMax - radius, rect.yMin + radius);
        case 1:
          return new Vector2(rect.xMax - radius, rect.yMax - radius);
        case 2:
          return new Vector2(rect.xMin + radius, rect.yMax - radius);
        default:
          return new Vector2(rect.xMin + radius, rect.yMin + radius);
      }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
      borderWidth = Mathf.Max(0f, borderWidth);
      cornerRadius = Mathf.Max(0f, cornerRadius);
      cornerSegments = Mathf.Clamp(cornerSegments, 2, 16);
      base.OnValidate();
      SetVerticesDirty();
    }
#endif
  }
}
