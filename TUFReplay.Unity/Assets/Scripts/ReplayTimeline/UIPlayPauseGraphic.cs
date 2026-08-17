using UnityEngine;
using UnityEngine.UI;

namespace TUFReplay.Unity.ReplayTimeline
{
  [RequireComponent(typeof(CanvasRenderer))]
  [AddComponentMenu("UI/TUFReplay/Play Pause Graphic")]
  public sealed class UIPlayPauseGraphic : MaskableGraphic
  {
    [SerializeField]
    private bool playing;

    [SerializeField]
    private Color discColor = new Color32(124, 207, 0, 255);

    [SerializeField]
    private Color iconColor = new Color32(53, 83, 14, 255);

    public void Configure(Color disc, Color icon)
    {
      discColor = disc;
      iconColor = icon;
      SetVerticesDirty();
    }

    public void SetPlaying(bool value)
    {
      if (playing == value)
        return;

      playing = value;
      SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
      vertexHelper.Clear();
      Rect rect = GetPixelAdjustedRect();
      Vector2 center = rect.center;
      float discDiameter = Mathf.Min(rect.width, rect.height);
      float iconScale = discDiameter / 42f;
      AddCircle(vertexHelper, center, discDiameter * 0.5f, discColor, 64);

      if (playing)
      {
        AddRoundedBar(
          vertexHelper,
          center + new Vector2(-5f * iconScale, 0f),
          5f * iconScale,
          21f * iconScale,
          iconColor
        );
        AddRoundedBar(
          vertexHelper,
          center + new Vector2(5f * iconScale, 0f),
          5f * iconScale,
          21f * iconScale,
          iconColor
        );
      }
      else
      {
        AddRoundedTriangle(vertexHelper, center + new Vector2(2f * iconScale, 0f), iconColor, iconScale);
      }
    }

    private static void AddRoundedTriangle(
      VertexHelper vertexHelper,
      Vector2 center,
      Color32 triangleColor,
      float scale
    )
    {
      Vector2 bottomLeft = center + new Vector2(-8f, -12f) * scale;
      Vector2 right = center + new Vector2(12f, 0f) * scale;
      Vector2 topLeft = center + new Vector2(-8f, 12f) * scale;
      float roundingDistance = 4f * scale;
      const int cornerSegments = 5;
      const int perimeterCount = cornerSegments * 3;

      int centerIndex = vertexHelper.currentVertCount;
      vertexHelper.AddVert(center, triangleColor, new Vector2(0.5f, 0.5f));
      AddRoundedCorner(vertexHelper, bottomLeft, topLeft, right, roundingDistance, cornerSegments, triangleColor);
      AddRoundedCorner(vertexHelper, right, bottomLeft, topLeft, roundingDistance, cornerSegments, triangleColor);
      AddRoundedCorner(vertexHelper, topLeft, right, bottomLeft, roundingDistance, cornerSegments, triangleColor);

      for (int index = 0; index < perimeterCount; index++)
      {
        int current = centerIndex + 1 + index;
        int next = centerIndex + 1 + ((index + 1) % perimeterCount);
        vertexHelper.AddTriangle(centerIndex, current, next);
      }
    }

    private static void AddRoundedCorner(
      VertexHelper vertexHelper,
      Vector2 vertex,
      Vector2 previous,
      Vector2 next,
      float distance,
      int segments,
      Color32 cornerColor
    )
    {
      Vector2 start = vertex + (previous - vertex).normalized * distance;
      Vector2 end = vertex + (next - vertex).normalized * distance;
      for (int index = 0; index < segments; index++)
      {
        float t = index / (float)(segments - 1);
        float inverse = 1f - t;
        Vector2 point = inverse * inverse * start + 2f * inverse * t * vertex + t * t * end;
        vertexHelper.AddVert(point, cornerColor, Vector2.zero);
      }
    }

    private static void AddRoundedBar(
      VertexHelper vertexHelper,
      Vector2 center,
      float width,
      float height,
      Color32 barColor
    )
    {
      float radius = width * 0.5f;
      float coreHeight = Mathf.Max(0f, height - width);
      AddQuad(vertexHelper, center, new Vector2(width, coreHeight), barColor);
      AddCircle(vertexHelper, center + Vector2.up * coreHeight * 0.5f, radius, barColor, 16);
      AddCircle(vertexHelper, center - Vector2.up * coreHeight * 0.5f, radius, barColor, 16);
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

    private static void AddQuad(VertexHelper vertexHelper, Vector2 center, Vector2 size, Color32 quadColor)
    {
      Vector2 half = size * 0.5f;
      int index = vertexHelper.currentVertCount;
      vertexHelper.AddVert(center + new Vector2(-half.x, -half.y), quadColor, Vector2.zero);
      vertexHelper.AddVert(center + new Vector2(-half.x, half.y), quadColor, Vector2.up);
      vertexHelper.AddVert(center + new Vector2(half.x, half.y), quadColor, Vector2.one);
      vertexHelper.AddVert(center + new Vector2(half.x, -half.y), quadColor, Vector2.right);
      vertexHelper.AddTriangle(index, index + 1, index + 2);
      vertexHelper.AddTriangle(index, index + 2, index + 3);
    }
  }
}
