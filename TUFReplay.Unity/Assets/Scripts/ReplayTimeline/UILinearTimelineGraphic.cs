using UnityEngine;
using UnityEngine.UI;

namespace TUFReplay.Unity.ReplayTimeline
{
  [RequireComponent(typeof(CanvasRenderer))]
  [AddComponentMenu("UI/TUFReplay/Linear Timeline Graphic")]
  public sealed class UILinearTimelineGraphic : MaskableGraphic
  {
    [SerializeField, Range(0f, 1f)]
    private float progress = 84f / 228f;

    [SerializeField]
    private Color trackColor = new Color32(43, 45, 52, 210);

    [SerializeField]
    private Color progressStartColor = new Color32(66, 224, 205, 255);

    [SerializeField]
    private Color progressMiddleColor = new Color32(70, 184, 255, 255);

    [SerializeField]
    private Color progressEndColor = new Color32(102, 119, 255, 255);

    [SerializeField, Range(4, 32)]
    private int gradientSegments = 16;

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

    public void Configure(Color background, Color start, Color middle, Color end)
    {
      trackColor = background;
      progressStartColor = start;
      progressMiddleColor = middle;
      progressEndColor = end;
      SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
      vertexHelper.Clear();
      Rect rect = GetPixelAdjustedRect();
      if (rect.width <= 0f || rect.height <= 0f)
        return;

      AddSolidPill(vertexHelper, rect, trackColor);

      float fillWidth = rect.width * progress;
      if (fillWidth <= 0f)
        return;

      AddProgressPill(vertexHelper, rect, fillWidth);
    }

    private static void AddSolidPill(VertexHelper vertexHelper, Rect rect, Color32 pillColor)
    {
      float radius = Mathf.Min(rect.height * 0.5f, rect.width * 0.5f);
      if (radius <= 0f)
        return;

      if (rect.width <= rect.height)
      {
        AddCircle(vertexHelper, rect.center, radius, pillColor, 16);
        return;
      }

      float leftX = rect.xMin + radius;
      float rightX = rect.xMax - radius;
      AddCircle(vertexHelper, new Vector2(leftX, rect.center.y), radius, pillColor, 16);
      AddCircle(vertexHelper, new Vector2(rightX, rect.center.y), radius, pillColor, 16);
      AddGradientQuad(vertexHelper, leftX, rightX, rect.yMin, rect.yMax, pillColor, pillColor);
    }

    private void AddProgressPill(VertexHelper vertexHelper, Rect fullRect, float fillWidth)
    {
      Rect fillRect = new Rect(fullRect.xMin, fullRect.yMin, fillWidth, fullRect.height);
      float radius = Mathf.Min(fillRect.height * 0.5f, fillRect.width * 0.5f);
      if (radius <= 0f)
        return;

      if (fillRect.width <= fillRect.height)
      {
        AddCircle(vertexHelper, fillRect.center, radius, EvaluateGradient(progress * 0.5f), 16);
        return;
      }

      float leftX = fillRect.xMin + radius;
      float rightX = fillRect.xMax - radius;
      AddCircle(
        vertexHelper,
        new Vector2(leftX, fillRect.center.y),
        radius,
        EvaluateGradient((leftX - fullRect.xMin) / fullRect.width),
        16
      );
      AddCircle(
        vertexHelper,
        new Vector2(rightX, fillRect.center.y),
        radius,
        EvaluateGradient((rightX - fullRect.xMin) / fullRect.width),
        16
      );

      int segmentCount = Mathf.Max(1, gradientSegments);
      for (int index = 0; index < segmentCount; index++)
      {
        float t0 = index / (float)segmentCount;
        float t1 = (index + 1f) / segmentCount;
        float x0 = Mathf.Lerp(leftX, rightX, t0);
        float x1 = Mathf.Lerp(leftX, rightX, t1);
        Color32 color0 = EvaluateGradient((x0 - fullRect.xMin) / fullRect.width);
        Color32 color1 = EvaluateGradient((x1 - fullRect.xMin) / fullRect.width);
        AddGradientQuad(vertexHelper, x0, x1, fillRect.yMin, fillRect.yMax, color0, color1);
      }
    }

    private Color EvaluateGradient(float normalized)
    {
      float t = Mathf.Clamp01(normalized);
      return t < 0.5f
        ? Color.Lerp(progressStartColor, progressMiddleColor, t * 2f)
        : Color.Lerp(progressMiddleColor, progressEndColor, (t - 0.5f) * 2f);
    }

    private static void AddGradientQuad(
      VertexHelper vertexHelper,
      float xMin,
      float xMax,
      float yMin,
      float yMax,
      Color32 left,
      Color32 right
    )
    {
      int start = vertexHelper.currentVertCount;
      vertexHelper.AddVert(new Vector2(xMin, yMin), left, Vector2.zero);
      vertexHelper.AddVert(new Vector2(xMin, yMax), left, Vector2.up);
      vertexHelper.AddVert(new Vector2(xMax, yMax), right, Vector2.one);
      vertexHelper.AddVert(new Vector2(xMax, yMin), right, Vector2.right);
      vertexHelper.AddTriangle(start, start + 1, start + 2);
      vertexHelper.AddTriangle(start, start + 2, start + 3);
    }

    private static void AddCircle(
      VertexHelper vertexHelper,
      Vector2 center,
      float radius,
      Color32 circleColor,
      int segments
    )
    {
      int centerIndex = vertexHelper.currentVertCount;
      vertexHelper.AddVert(center, circleColor, new Vector2(0.5f, 0.5f));
      for (int index = 0; index <= segments; index++)
      {
        float angle = Mathf.PI * 2f * index / segments;
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        vertexHelper.AddVert(center + direction * radius, circleColor, direction * 0.5f + Vector2.one * 0.5f);
        if (index > 0)
          vertexHelper.AddTriangle(centerIndex, centerIndex + index, centerIndex + index + 1);
      }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
      progress = Mathf.Clamp01(progress);
      gradientSegments = Mathf.Clamp(gradientSegments, 4, 32);
      base.OnValidate();
      SetVerticesDirty();
    }
#endif
  }
}
