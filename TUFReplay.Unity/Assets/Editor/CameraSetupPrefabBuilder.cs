using System;
using System.IO;
using TMPro;
using TUFReplay.Unity.CameraSetup;
using TUFReplay.Unity.ReplayTimeline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TUFReplay.Unity.Editor
{
  public static class CameraSetupPrefabBuilder
  {
    public const string PrefabPath = "Assets/Prefabs/CameraSetupRuntime.prefab";
    public const string PreviewScenePath = "Assets/Scenes/CameraSetupPreview.unity";
    private static readonly Color White = new Color32(250, 250, 250, 255);
    private static readonly Color Muted = new Color32(161, 161, 170, 255);
    private static readonly Color Accent = new Color32(124, 207, 0, 255);

    [MenuItem("Tools/TUFReplay/Rebuild Camera Setup Modal")]
    public static void Rebuild()
    {
      TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
        ReplayTimelinePrototypeBuilder.TimelineTmpFontPath
      );
      if (font == null)
        throw new InvalidOperationException("Rebuild the replay timeline prototype to create its shared font first.");
      if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        AssetDatabase.CreateFolder("Assets", "Prefabs");
      var root = new GameObject("CameraSetupRuntime", typeof(RectTransform), typeof(CanvasGroup));
      Stretch(root.GetComponent<RectTransform>());
      Image scrim = root.AddComponent<Image>();
      scrim.color = new Color(0, 0, 0, 0.68f);
      GameObject panel = Rect("Panel", root.transform, new Vector2(900, 540), Vector2.zero);
      UIRoundedPanelGraphic surface = panel.AddComponent<UIRoundedPanelGraphic>();
      surface.Configure(new Color32(17, 20, 27, 255), new Color(1, 1, 1, 0.14f), 1, 20);
      TMP_Text heading = Text(
        "Heading",
        panel.transform,
        font,
        "Check your camera before playing",
        new Vector2(828, 46),
        new Vector2(0, 214),
        27,
        White
      );
      TMP_Text description = Text(
        "Description",
        panel.transform,
        font,
        "You’ll see this once each time you open the game.",
        new Vector2(828, 30),
        new Vector2(0, 173),
        16,
        Muted
      );
      GameObject previewArea = Rect("PreviewArea", panel.transform, new Vector2(414, 312), new Vector2(-207, -16));
      UIRoundedPanelGraphic previewSurface = previewArea.AddComponent<UIRoundedPanelGraphic>();
      previewSurface.Configure(new Color32(8, 10, 15, 255), new Color(1, 1, 1, 0.08f), 1, 12);
      GameObject imageObject = Rect("Preview", previewArea.transform, new Vector2(414, 312), Vector2.zero);
      RawImage image = imageObject.AddComponent<RawImage>();
      image.raycastTarget = false;
      image.color = Color.clear;
      image.enabled = false;
      var fit = imageObject.AddComponent<AspectRatioFitter>();
      fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
      fit.aspectRatio = 4f / 3f;
      TMP_Text placeholder = Text(
        "PreviewHint",
        previewArea.transform,
        font,
        "Camera is off",
        new Vector2(360, 100),
        Vector2.zero,
        19,
        Muted
      );
      placeholder.alignment = TextAlignmentOptions.Center;
      Button reveal = ButtonControl(
        "RevealButton",
        previewArea.transform,
        font,
        "Click to reveal",
        new Vector2(414, 312),
        Vector2.zero,
        false
      );
      Stretch(reveal.GetComponent<RectTransform>());
      TMP_Text revealLabel = reveal.GetComponentInChildren<TMP_Text>();
      revealLabel.fontSize = 20;
      Stretch(revealLabel.rectTransform);
      revealLabel.rectTransform.offsetMin = new Vector2(24, 24);
      revealLabel.rectTransform.offsetMax = new Vector2(-24, -24);
      TMP_Text device = Text(
        "DeviceName",
        panel.transform,
        font,
        "Default camera",
        new Vector2(380, 30),
        new Vector2(-207, -190),
        15,
        Muted
      );
      device.alignment = TextAlignmentOptions.Center;
      TMP_Text cameraLabel = Text(
        "CameraLabel",
        panel.transform,
        font,
        "Camera on",
        new Vector2(290, 38),
        new Vector2(165, 114),
        19,
        White
      );
      Toggle camera = ToggleControl("CameraToggle", panel.transform, new Vector2(385, 114));
      TMP_Text liveLabel = Text(
        "LiveLabel",
        panel.transform,
        font,
        "Show live camera while playing",
        new Vector2(290, 44),
        new Vector2(165, 58),
        17,
        White
      );
      Toggle live = ToggleControl("LiveToggle", panel.transform, new Vector2(385, 58));
      TMP_Text privacy = Text(
        "PrivacyHint",
        panel.transform,
        font,
        "Your camera stays active while the game is open. Video is saved only during runs.",
        new Vector2(390, 168),
        new Vector2(215, -56),
        16,
        Muted
      );
      privacy.alignment = TextAlignmentOptions.TopLeft;
      TMP_Text error = Text(
        "ErrorText",
        panel.transform,
        font,
        "",
        new Vector2(390, 74),
        new Vector2(215, -176),
        14,
        new Color32(242, 184, 75, 255)
      );
      error.gameObject.SetActive(false);
      Button retry = ButtonControl(
        "RetryButton",
        previewArea.transform,
        font,
        "Reconnect",
        new Vector2(130, 36),
        new Vector2(0, -106),
        false
      );
      retry.gameObject.SetActive(false);
      Button cancel = ButtonControl(
        "CancelButton",
        panel.transform,
        font,
        "Back",
        new Vector2(100, 42),
        new Vector2(-362, -230),
        false
      );
      Button without = ButtonControl(
        "WithoutCameraButton",
        panel.transform,
        font,
        "Start without camera",
        new Vector2(210, 42),
        new Vector2(102, -230),
        false
      );
      Button start = ButtonControl(
        "ContinueButton",
        panel.transform,
        font,
        "Start run",
        new Vector2(184, 42),
        new Vector2(320, -230),
        true
      );
      var view = root.AddComponent<CameraSetupView>();
      view.Configure(
        image,
        heading,
        description,
        placeholder,
        device,
        cameraLabel,
        liveLabel,
        privacy,
        error,
        camera,
        live,
        start,
        without,
        cancel,
        retry,
        reveal
      );
      root.SetActive(false);
      PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
      UnityEngine.Object.DestroyImmediate(root);
      AssetDatabase.SaveAssets();
      Debug.Log("[TUFReplay.Unity] Camera setup modal prefab rebuilt and configured.");
    }

    [MenuItem("Tools/TUFReplay/Preview Camera Setup Modal")]
    public static void Preview()
    {
      if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        return;
      if (File.Exists(PreviewScenePath))
      {
        EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
        CameraSetupPreviewDriver existing = UnityEngine.Object.FindFirstObjectByType<CameraSetupPreviewDriver>();
        if (existing == null || existing.View == null || !existing.View.IsConfigured)
          throw new InvalidOperationException("The saved camera preview scene is missing its configured camera view.");
        existing.Refresh();
        Selection.activeGameObject = existing.View.gameObject;
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        return;
      }
      GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
      if (prefab == null || prefab.GetComponent<CameraSetupView>()?.IsConfigured != true)
      {
        Rebuild();
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
      }
      var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
      var cameraObject = new GameObject("CameraSetupPreviewCamera", typeof(UnityEngine.Camera));
      UnityEngine.Camera camera = cameraObject.GetComponent<UnityEngine.Camera>();
      camera.clearFlags = CameraClearFlags.SolidColor;
      camera.backgroundColor = new Color32(23, 25, 31, 255);
      camera.cullingMask = 0;
      var canvasRoot = new GameObject(
        "CameraSetupPreviewCanvas",
        typeof(RectTransform),
        typeof(Canvas),
        typeof(CanvasScaler),
        typeof(GraphicRaycaster)
      );
      canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
      canvasRoot.GetComponent<Canvas>().sortingOrder = 32750;
      CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
      scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      scaler.referenceResolution = new Vector2(1920, 1080);
      scaler.matchWidthOrHeight = 0.5f;
      new GameObject("CameraSetupPreviewEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
      GameObject instance = PrefabUtility.InstantiatePrefab(prefab, canvasRoot.transform) as GameObject;
      var view = instance.GetComponent<CameraSetupView>();
      canvasRoot.AddComponent<CameraSetupPreviewDriver>().Configure(view);
      if (!EditorSceneManager.SaveScene(scene, PreviewScenePath))
        throw new IOException("The camera preview scene could not be saved. Check disk space and try again.");
      Selection.activeGameObject = instance;
      EditorApplication.ExecuteMenuItem("Window/General/Game");
      Debug.Log("[TUFReplay.Unity] Saved camera setup preview scene: " + PreviewScenePath);
    }

    private static GameObject Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
      var item = new GameObject(name, typeof(RectTransform));
      item.transform.SetParent(parent, false);
      var rect = item.GetComponent<RectTransform>();
      rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
      rect.sizeDelta = size;
      rect.anchoredPosition = position;
      return item;
    }

    private static void Stretch(RectTransform rect)
    {
      rect.anchorMin = Vector2.zero;
      rect.anchorMax = Vector2.one;
      rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static TMP_Text Text(
      string name,
      Transform parent,
      TMP_FontAsset font,
      string value,
      Vector2 size,
      Vector2 position,
      float sizePx,
      Color color
    )
    {
      var text = Rect(name, parent, size, position).AddComponent<TextMeshProUGUI>();
      text.font = font;
      text.fontSize = sizePx;
      text.text = value;
      text.color = color;
      text.alignment = TextAlignmentOptions.Left;
      text.raycastTarget = false;
      text.textWrappingMode = TextWrappingModes.Normal;
      return text;
    }

    private static Toggle ToggleControl(string name, Transform parent, Vector2 position)
    {
      GameObject item = Rect(name, parent, new Vector2(48, 32), position);
      UIRoundedPanelGraphic track = item.AddComponent<UIRoundedPanelGraphic>();
      track.Configure(Color.white, Color.clear, 0, 16);
      var toggle = item.AddComponent<Toggle>();
      toggle.targetGraphic = track;
      toggle.transition = Selectable.Transition.None;
      GameObject knob = Rect("Thumb", item.transform, new Vector2(26, 26), new Vector2(-10, 0));
      UIFilledCircleGraphic graphic = knob.AddComponent<UIFilledCircleGraphic>();
      graphic.color = White;
      graphic.raycastTarget = false;
      toggle.isOn = false;
      item.AddComponent<CameraToggleView>().Configure(toggle, track, knob.GetComponent<RectTransform>());
      return toggle;
    }

    private static Button ButtonControl(
      string name,
      Transform parent,
      TMP_FontAsset font,
      string label,
      Vector2 size,
      Vector2 position,
      bool primary
    )
    {
      GameObject item = Rect(name, parent, size, position);
      UIRoundedPanelGraphic surface = item.AddComponent<UIRoundedPanelGraphic>();
      surface.Configure(
        primary ? Accent : new Color32(33, 37, 45, 255),
        primary ? Color.clear : new Color(1, 1, 1, 0.08f),
        1,
        9
      );
      Button button = item.AddComponent<Button>();
      button.targetGraphic = surface;
      TMP_Text text = Text(
        "Label",
        item.transform,
        font,
        label,
        size - new Vector2(14, 0),
        Vector2.zero,
        15,
        primary ? new Color32(30, 48, 6, 255) : White
      );
      text.alignment = TextAlignmentOptions.Center;
      return button;
    }
  }
}
