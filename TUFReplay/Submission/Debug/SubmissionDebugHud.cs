using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace TUFReplay.Submission.Debug;

/// <summary>Small code-built uGUI HUD used while exercising the real mod upload path.</summary>
internal sealed class SubmissionDebugHud : MonoBehaviour
{
  private const int SortOrder = 31990;
  private const int VisibleEventCount = 8;
  private static SubmissionDebugHud _instance;

  private readonly Queue<string> _visibleEvents = new Queue<string>(VisibleEventCount);
  private readonly StringBuilder _textBuilder = new StringBuilder(768);
  private GameObject _panel;
  private Text _events;
  private Text _transfer;

  internal static void Initialize()
  {
    Shutdown();

    try
    {
      var canvasObject = new GameObject(
        "TUFReplaySubmissionDebugCanvas",
        typeof(RectTransform),
        typeof(Canvas),
        typeof(CanvasScaler),
        typeof(SubmissionDebugHud)
      );
      DontDestroyOnLoad(canvasObject);

      Canvas canvas = canvasObject.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.overrideSorting = true;
      canvas.sortingOrder = SortOrder;

      CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
      scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      scaler.referenceResolution = new Vector2(1920f, 1080f);
      scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
      scaler.matchWidthOrHeight = 0.5f;

      SubmissionDebugHud hud = canvasObject.GetComponent<SubmissionDebugHud>();
      hud.Build();
      _instance = hud;
      SubmissionDebugTelemetry.Publish("Debug HUD ready; waiting for a TUF level");
    }
    catch (System.Exception exception)
    {
      Main.Instance?.LogException("SubmissionDebugHud.Initialize", exception);
      Shutdown();
    }
  }

  internal static void Shutdown()
  {
    SubmissionDebugHud instance = _instance;
    _instance = null;
    if (instance?.gameObject != null)
      Destroy(instance.gameObject);
  }

  internal static void SetVisible(bool visible)
  {
    if (_instance?._panel != null)
      _instance._panel.SetActive(visible);
  }

  private void Build()
  {
    Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

    _panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
    _panel.transform.SetParent(transform, false);
    RectTransform panelRect = _panel.GetComponent<RectTransform>();
    panelRect.anchorMin = new Vector2(1f, 1f);
    panelRect.anchorMax = new Vector2(1f, 1f);
    panelRect.pivot = new Vector2(1f, 1f);
    panelRect.anchoredPosition = new Vector2(-18f, -18f);
    panelRect.sizeDelta = new Vector2(600f, 286f);
    _panel.GetComponent<Image>().color = new Color(0.025f, 0.03f, 0.045f, 0.88f);

    Text title = CreateText("Title", _panel.transform, font, 20, FontStyle.Bold);
    RectTransform titleRect = title.rectTransform;
    titleRect.anchorMin = new Vector2(0f, 1f);
    titleRect.anchorMax = new Vector2(1f, 1f);
    titleRect.pivot = new Vector2(0.5f, 1f);
    titleRect.offsetMin = new Vector2(18f, -52f);
    titleRect.offsetMax = new Vector2(-18f, -14f);
    title.alignment = TextAnchor.MiddleLeft;
    title.color = new Color(0.36f, 0.92f, 0.78f, 1f);
    title.text = "TUFReplay · Auto submission debug";

    _events = CreateText("Events", _panel.transform, font, 17, FontStyle.Normal);
    RectTransform eventsRect = _events.rectTransform;
    eventsRect.anchorMin = Vector2.zero;
    eventsRect.anchorMax = Vector2.one;
    eventsRect.offsetMin = new Vector2(18f, 48f);
    eventsRect.offsetMax = new Vector2(-18f, -58f);
    _events.alignment = TextAnchor.UpperLeft;
    _events.horizontalOverflow = HorizontalWrapMode.Wrap;
    _events.verticalOverflow = VerticalWrapMode.Truncate;
    _events.color = new Color(0.88f, 0.9f, 0.94f, 1f);

    _transfer = CreateText("Transfer", _panel.transform, font, 15, FontStyle.Normal);
    RectTransform transferRect = _transfer.rectTransform;
    transferRect.anchorMin = new Vector2(0f, 0f);
    transferRect.anchorMax = new Vector2(1f, 0f);
    transferRect.pivot = new Vector2(0.5f, 0f);
    transferRect.offsetMin = new Vector2(18f, 12f);
    transferRect.offsetMax = new Vector2(-18f, 42f);
    _transfer.alignment = TextAnchor.MiddleLeft;
    _transfer.color = new Color(0.62f, 0.68f, 0.76f, 1f);
    _transfer.text = "No buffer sent yet";
  }

  private static Text CreateText(string name, Transform parent, Font font, int size, FontStyle style)
  {
    var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
    textObject.transform.SetParent(parent, false);
    Text text = textObject.GetComponent<Text>();
    text.font = font;
    text.fontSize = size;
    text.fontStyle = style;
    text.raycastTarget = false;
    return text;
  }

  private void Update()
  {
    bool changed = false;
    while (SubmissionDebugTelemetry.TryTake(out SubmissionDebugTelemetry.Message message))
    {
      if (message.IsTransfer)
      {
        if (_transfer != null)
          _transfer.text = message.Text;
        continue;
      }
      if (_visibleEvents.Count == VisibleEventCount)
        _visibleEvents.Dequeue();
      _visibleEvents.Enqueue(message.Text);
      changed = true;
    }
    if (!changed || _events == null)
      return;

    _textBuilder.Clear();
    foreach (string message in _visibleEvents)
      _textBuilder.AppendLine(message);
    _events.text = _textBuilder.ToString();
  }
}
