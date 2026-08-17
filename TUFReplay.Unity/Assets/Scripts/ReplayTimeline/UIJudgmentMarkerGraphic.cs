using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TUFReplay.Unity.ReplayTimeline
{
  public enum ReplayJudgmentKind
  {
    Overload,
    TooEarly,
    Early,
    EarlyPerfect,
    Perfect,
    LatePerfect,
    Late,
    TooLate,
    Miss,
  }

  [Serializable]
  public struct ReplayJudgmentMarker
  {
    [Range(0f, 1f)]
    public float normalizedTime;

    public ReplayJudgmentKind judgment;

    public ReplayJudgmentMarker(float time, ReplayJudgmentKind kind)
    {
      normalizedTime = Mathf.Clamp01(time);
      judgment = kind;
    }
  }

  public static class ReplayJudgmentPalette
  {
    public static Color32 GetColor(ReplayJudgmentKind judgment)
    {
      switch (judgment)
      {
        case ReplayJudgmentKind.Overload:
        case ReplayJudgmentKind.Miss:
          return new Color32(217, 88, 255, 255); // ImplResourcePack #D958FF
        case ReplayJudgmentKind.TooEarly:
        case ReplayJudgmentKind.TooLate:
          return new Color32(255, 0, 0, 255); // ImplResourcePack #FF0000
        case ReplayJudgmentKind.Early:
        case ReplayJudgmentKind.Late:
          return new Color32(255, 111, 78, 255); // ImplResourcePack #FF6F4E
        case ReplayJudgmentKind.EarlyPerfect:
        case ReplayJudgmentKind.LatePerfect:
          return new Color32(160, 255, 78, 255); // ImplResourcePack #A0FF4E
        case ReplayJudgmentKind.Perfect:
          return new Color32(96, 255, 78, 255); // ImplResourcePack #60FF4E
        default:
          return Color.white;
      }
    }
  }

  [RequireComponent(typeof(CanvasRenderer))]
  [AddComponentMenu("UI/TUFReplay/Judgment Marker Graphic")]
  public sealed class UIJudgmentMarkerGraphic : MaskableGraphic
  {
    [SerializeField, Min(1f)]
    private float markerWidth = 2f;

    [SerializeField, Min(1f)]
    private float markerHeight = 8f;

    [SerializeField]
    private float markerBottom = 9f;

    [SerializeField]
    private ReplayJudgmentMarker[] markers = Array.Empty<ReplayJudgmentMarker>();

    [SerializeField]
    private int visibleMask;

    public int VisibleMask
    {
      get => visibleMask;
      set
      {
        if (visibleMask == value)
          return;
        visibleMask = value;
        SetVerticesDirty();
      }
    }

    public void SetMarkers(IReadOnlyList<ReplayJudgmentMarker> source)
    {
      if (source == null || source.Count == 0)
      {
        markers = Array.Empty<ReplayJudgmentMarker>();
      }
      else
      {
        markers = new ReplayJudgmentMarker[source.Count];
        for (int index = 0; index < source.Count; index++)
          markers[index] = source[index];
      }
      SetVerticesDirty();
    }

    public void Configure(float width, float height, float bottom)
    {
      markerWidth = Mathf.Max(1f, width);
      markerHeight = Mathf.Max(1f, height);
      markerBottom = bottom;
      raycastTarget = false;
      SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
      vertexHelper.Clear();
      Rect rect = GetPixelAdjustedRect();
      if (rect.width <= 0f || rect.height <= 0f || visibleMask == 0 || markers == null)
        return;

      float halfWidth = markerWidth * 0.5f;
      float bottom = rect.center.y + markerBottom;
      float top = bottom + markerHeight;
      for (int index = 0; index < markers.Length; index++)
      {
        ReplayJudgmentMarker marker = markers[index];
        int bit = 1 << (int)marker.judgment;
        if ((visibleMask & bit) == 0)
          continue;

        float x = Mathf.Lerp(rect.xMin, rect.xMax, Mathf.Clamp01(marker.normalizedTime));
        AddQuad(vertexHelper, x - halfWidth, x + halfWidth, bottom, top, ReplayJudgmentPalette.GetColor(marker.judgment));
      }
    }

    private static void AddQuad(
      VertexHelper vertexHelper,
      float xMin,
      float xMax,
      float yMin,
      float yMax,
      Color32 markerColor
    )
    {
      int start = vertexHelper.currentVertCount;
      vertexHelper.AddVert(new Vector2(xMin, yMin), markerColor, Vector2.zero);
      vertexHelper.AddVert(new Vector2(xMin, yMax), markerColor, Vector2.up);
      vertexHelper.AddVert(new Vector2(xMax, yMax), markerColor, Vector2.one);
      vertexHelper.AddVert(new Vector2(xMax, yMin), markerColor, Vector2.right);
      vertexHelper.AddTriangle(start, start + 1, start + 2);
      vertexHelper.AddTriangle(start, start + 2, start + 3);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
      markerWidth = Mathf.Max(1f, markerWidth);
      markerHeight = Mathf.Max(1f, markerHeight);
      base.OnValidate();
      SetVerticesDirty();
    }
#endif
  }
}
