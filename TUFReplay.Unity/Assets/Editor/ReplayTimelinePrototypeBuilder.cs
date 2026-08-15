using System;
using System.Linq;
using TMPro;
using TUFReplay.Unity.ReplayTimeline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace TUFReplay.Unity.Editor
{
  public static class ReplayTimelinePrototypeBuilder
  {
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string FontFolder = "Assets/Fonts";
    private const string TimelineFontPath = FontFolder + "/MAPLESTORY_OTF_BOLD.OTF";
    internal const string TimelineTmpFontPath = FontFolder + "/MAPLESTORY_OTF_BOLD Dynamic SDF.asset";

    private static readonly Color WarmWhite = new Color(1f, 0.97f, 0.9f, 1f);
    private static readonly Color PanelFill = new Color(0.071f, 0.078f, 0.114f, 0.82f);
    private static readonly Color Accent = new Color32(70, 184, 255, 255);
    private static readonly Color DarkIcon = new Color32(16, 17, 22, 255);

    [MenuItem("Tools/TUFReplay/Rebuild Replay Timeline Prototype")]
    public static void Rebuild()
    {
      if (EditorApplication.isPlayingOrWillChangePlaymode)
      {
        Debug.LogWarning("[TUFReplay.Unity] Exit Play Mode before rebuilding the replay timeline prototype.");
        return;
      }

      EnsureFolders();
      AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
      TMP_FontAsset timelineFont = EnsureTimelineFontAsset();
      GameObject playButtonPrefab = BuildPlayButtonPrefab();

      UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
      CleanupGeneratedSceneRoots(scene);

      Camera camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
      if (camera == null)
      {
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
        camera = cameraObject.GetComponent<Camera>();
        cameraObject.tag = "MainCamera";
      }
      camera.clearFlags = CameraClearFlags.SolidColor;
      camera.backgroundColor = Html("#0B0D14");

      Canvas canvas = BuildCanvas();
      BuildEventSystem();

      string timelinePrefabPath = $"{PrefabFolder}/ReplayTimeline.prefab";
      AssetDatabase.DeleteAsset(timelinePrefabPath);
      GameObject timelineRoot = BuildTimeline(canvas.transform, playButtonPrefab, timelineFont);
      foreach (Graphic graphic in timelineRoot.GetComponentsInChildren<Graphic>(true))
        graphic.SetAllDirty();
      Canvas.ForceUpdateCanvases();

      PrefabUtility.SaveAsPrefabAsset(timelineRoot, timelinePrefabPath);
      UnityEngine.Object.DestroyImmediate(timelineRoot);
      GameObject timelinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(timelinePrefabPath);
      GameObject connectedRoot = PrefabUtility.InstantiatePrefab(timelinePrefab, canvas.transform) as GameObject;

      GameObject driverObject = new GameObject("ReplayTimelinePreviewDriver", typeof(ReplayTimelinePreviewDriver));
      driverObject.transform.SetParent(canvas.transform, false);
      driverObject
        .GetComponent<ReplayTimelinePreviewDriver>()
        .Configure(connectedRoot.GetComponent<ReplayTimelineView>());

      foreach (Graphic graphic in connectedRoot.GetComponentsInChildren<Graphic>(true))
        graphic.SetAllDirty();
      Canvas.ForceUpdateCanvases();
      EditorSceneManager.MarkSceneDirty(scene);
      EditorSceneManager.SaveScene(scene, ScenePath);
      AssetDatabase.SaveAssets();
      Selection.activeGameObject = null;
      Debug.Log("[TUFReplay.Unity] Rebuilt the TUFHelperLite-style linear replay timeline prototype.");
    }

    private static GameObject BuildTimeline(Transform parent, GameObject playButtonPrefab, TMP_FontAsset timelineFont)
    {
      GameObject root = CreateUIObject("ReplayTimelinePreview", parent);
      RectTransform rootRect = root.GetComponent<RectTransform>();
      rootRect.anchorMin = Vector2.zero;
      rootRect.anchorMax = Vector2.one;
      rootRect.offsetMin = Vector2.zero;
      rootRect.offsetMax = Vector2.zero;

      GameObject floatingPanel = CreateUIObject("FloatingPanel", root.transform);
      RectTransform floatingPanelRect = floatingPanel.GetComponent<RectTransform>();
      floatingPanelRect.anchorMin = floatingPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
      floatingPanelRect.pivot = new Vector2(0.5f, 0.5f);
      floatingPanelRect.anchoredPosition = new Vector2(0f, -388f);
      floatingPanelRect.sizeDelta = new Vector2(520f, 112f);

      GameObject shadowObject = CreateUIObject("PanelShadow", floatingPanel.transform);
      RectTransform shadowRect = shadowObject.GetComponent<RectTransform>();
      shadowRect.anchorMin = shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
      shadowRect.sizeDelta = new Vector2(520f, 112f);
      shadowRect.anchoredPosition = new Vector2(0f, -6f);
      UIRoundedPanelGraphic shadow = shadowObject.AddComponent<UIRoundedPanelGraphic>();
      shadow.Configure(new Color(0f, 0f, 0f, 0.3f), Color.clear, 0f, 16f);
      shadow.raycastTarget = false;

      GameObject panelObject = CreateUIObject("PanelBackground", floatingPanel.transform);
      RectTransform panelRect = panelObject.GetComponent<RectTransform>();
      panelRect.anchorMin = Vector2.zero;
      panelRect.anchorMax = Vector2.one;
      panelRect.offsetMin = Vector2.zero;
      panelRect.offsetMax = Vector2.zero;
      UIRoundedPanelGraphic panel = panelObject.AddComponent<UIRoundedPanelGraphic>();
      panel.Configure(PanelFill, new Color(WarmWhite.r, WarmWhite.g, WarmWhite.b, 0.14f), 2f, 16f);
      panel.raycastTarget = true;

      GameObject sheenObject = CreateUIObject("TopSheen", floatingPanel.transform);
      Image sheen = sheenObject.AddComponent<Image>();
      sheen.color = new Color(1f, 1f, 1f, 0.12f);
      sheen.raycastTarget = false;
      RectTransform sheenRect = sheen.rectTransform;
      sheenRect.anchorMin = new Vector2(0f, 1f);
      sheenRect.anchorMax = new Vector2(1f, 1f);
      sheenRect.pivot = new Vector2(0.5f, 1f);
      sheenRect.anchoredPosition = new Vector2(0f, -1f);
      sheenRect.sizeDelta = new Vector2(-36f, 1f);

      TextMeshProUGUI elapsedText = BuildText(
        "ElapsedTime",
        floatingPanel.transform,
        "01:24",
        18f,
        new Color(WarmWhite.r, WarmWhite.g, WarmWhite.b, 0.92f),
        timelineFont,
        TextAlignmentOptions.Center
      );
      SetCenteredRect(elapsedText.rectTransform, new Vector2(-220f, 23f), new Vector2(56f, 26f));

      TextMeshProUGUI durationText = BuildText(
        "DurationTime",
        floatingPanel.transform,
        "03:48",
        18f,
        new Color(WarmWhite.r, WarmWhite.g, WarmWhite.b, 0.68f),
        timelineFont,
        TextAlignmentOptions.Center
      );
      SetCenteredRect(durationText.rectTransform, new Vector2(220f, 23f), new Vector2(56f, 26f));

      GameObject timelineHitObject = CreateUIObject("TimelineHitArea", floatingPanel.transform);
      RectTransform timelineHitRect = timelineHitObject.GetComponent<RectTransform>();
      SetCenteredRect(timelineHitRect, new Vector2(0f, 23f), new Vector2(344f, 34f));
      Image timelineHitImage = timelineHitObject.AddComponent<Image>();
      timelineHitImage.color = Color.clear;
      timelineHitImage.raycastTarget = true;
      UITimelineSeekInput seekInput = timelineHitObject.AddComponent<UITimelineSeekInput>();
      seekInput.Configure(timelineHitRect);

      GameObject trackObject = CreateUIObject("LinearTimeline", timelineHitObject.transform);
      RectTransform trackRect = trackObject.GetComponent<RectTransform>();
      SetCenteredRect(trackRect, Vector2.zero, new Vector2(344f, 14f));
      UILinearTimelineGraphic track = trackObject.AddComponent<UILinearTimelineGraphic>();
      track.Configure(
        new Color32(43, 45, 52, 210),
        new Color32(66, 224, 205, 255),
        new Color32(70, 184, 255, 255),
        new Color32(102, 119, 255, 255)
      );
      track.raycastTarget = false;

      GameObject playheadObject = CreateUIObject("Playhead", trackObject.transform);
      RectTransform playheadRect = playheadObject.GetComponent<RectTransform>();
      playheadRect.anchorMin = playheadRect.anchorMax = new Vector2(84f / 228f, 0.5f);
      playheadRect.sizeDelta = new Vector2(18f, 18f);
      UIFilledCircleGraphic playhead = playheadObject.AddComponent<UIFilledCircleGraphic>();
      playhead.color = WarmWhite;
      playhead.raycastTarget = false;

      GameObject backwardObject = BuildSeekButton("SeekBackward", floatingPanel.transform, "−5s", timelineFont);
      RectTransform backwardRect = backwardObject.GetComponent<RectTransform>();
      SetCenteredRect(backwardRect, new Vector2(-66f, -18f), new Vector2(52f, 40f));

      GameObject playButton = PrefabUtility.InstantiatePrefab(playButtonPrefab, floatingPanel.transform) as GameObject;
      RectTransform playRect = playButton.GetComponent<RectTransform>();
      SetCenteredRect(playRect, new Vector2(0f, -18f), new Vector2(52f, 52f));

      GameObject forwardObject = BuildSeekButton("SeekForward", floatingPanel.transform, "+5s", timelineFont);
      RectTransform forwardRect = forwardObject.GetComponent<RectTransform>();
      SetCenteredRect(forwardRect, new Vector2(66f, -18f), new Vector2(52f, 40f));

      GameObject dockButtonObject = BuildCloseButton("DockButton", floatingPanel.transform);
      RectTransform dockButtonRect = dockButtonObject.GetComponent<RectTransform>();
      SetCenteredRect(dockButtonRect, new Vector2(226f, -18f), new Vector2(44f, 40f));

      UIFloatingPanelDragHandle dragHandle = floatingPanel.AddComponent<UIFloatingPanelDragHandle>();
      dragHandle.Configure(
        floatingPanelRect,
        new[] { backwardRect, playRect, forwardRect, timelineHitRect, dockButtonRect },
        16f
      );

      GameObject dockTabObject = BuildDockTab("DockTab", root.transform);
      RectTransform dockTabRect = dockTabObject.GetComponent<RectTransform>();
      SetCenteredRect(dockTabRect, new Vector2(980f, -388f), new Vector2(44f, 48f));
      CanvasGroup dockTabCanvasGroup = dockTabObject.AddComponent<CanvasGroup>();
      dockTabCanvasGroup.alpha = 0f;
      dockTabCanvasGroup.interactable = false;
      dockTabCanvasGroup.blocksRaycasts = false;

      UIFloatingPanelDockController dockController = root.AddComponent<UIFloatingPanelDockController>();
      dockController.Configure(
        rootRect,
        floatingPanelRect,
        dragHandle,
        dockButtonObject.GetComponent<Button>(),
        dockTabRect,
        dockTabCanvasGroup,
        dockTabObject.GetComponent<Button>()
      );

      ReplayTimelineView view = root.AddComponent<ReplayTimelineView>();
      view.Configure(
        track,
        playheadRect,
        seekInput,
        playButton.GetComponent<UIPlayPauseGraphic>(),
        playButton.GetComponent<Button>(),
        backwardObject.GetComponent<Button>(),
        forwardObject.GetComponent<Button>(),
        elapsedText,
        durationText,
        dockController
      );
      return root;
    }

    private static GameObject BuildPlayButtonPrefab()
    {
      GameObject buttonObject = CreateUIObject("PlayPause", null);
      buttonObject.GetComponent<RectTransform>().sizeDelta = new Vector2(52f, 52f);
      UIPlayPauseGraphic graphic = buttonObject.AddComponent<UIPlayPauseGraphic>();
      graphic.Configure(Accent, DarkIcon);
      graphic.raycastTarget = true;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = graphic;
      button.navigation = new Navigation { mode = Navigation.Mode.None };
      ColorBlock colors = button.colors;
      colors.normalColor = Color.white;
      colors.highlightedColor = new Color32(207, 242, 255, 255);
      colors.pressedColor = new Color32(157, 220, 246, 255);
      colors.selectedColor = Color.white;
      colors.disabledColor = new Color32(100, 103, 112, 170);
      colors.colorMultiplier = 1f;
      colors.fadeDuration = 0.12f;
      button.colors = colors;

      string path = $"{PrefabFolder}/PlayButton.prefab";
      PrefabUtility.SaveAsPrefabAsset(buttonObject, path);
      UnityEngine.Object.DestroyImmediate(buttonObject);
      AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
      return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static GameObject BuildSeekButton(string name, Transform parent, string label, TMP_FontAsset font)
    {
      GameObject buttonObject = CreateUIObject(name, parent);
      UIRoundedPanelGraphic background = buttonObject.AddComponent<UIRoundedPanelGraphic>();
      background.Configure(new Color32(24, 26, 33, 255), Color.clear, 0f, 11f);
      background.raycastTarget = true;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = background;
      button.navigation = new Navigation { mode = Navigation.Mode.None };
      ColorBlock colors = button.colors;
      colors.normalColor = Color.white;
      colors.highlightedColor = new Color32(207, 242, 255, 255);
      colors.pressedColor = new Color32(157, 220, 246, 255);
      colors.selectedColor = Color.white;
      colors.disabledColor = new Color32(100, 103, 112, 170);
      colors.colorMultiplier = 1f;
      colors.fadeDuration = 0.12f;
      button.colors = colors;

      TextMeshProUGUI text = BuildText(
        "Label",
        buttonObject.transform,
        label,
        18f,
        new Color32(224, 226, 232, 255),
        font,
        TextAlignmentOptions.Center
      );
      text.rectTransform.anchorMin = Vector2.zero;
      text.rectTransform.anchorMax = Vector2.one;
      text.rectTransform.offsetMin = Vector2.zero;
      text.rectTransform.offsetMax = Vector2.zero;
      return buttonObject;
    }

    private static GameObject BuildCloseButton(string name, Transform parent)
    {
      GameObject buttonObject = CreateUIObject(name, parent);
      UIRoundedPanelGraphic hitArea = buttonObject.AddComponent<UIRoundedPanelGraphic>();
      hitArea.Configure(Color.clear, Color.clear, 0f, 0f);
      hitArea.raycastTarget = true;

      GameObject visualObject = CreateUIObject("Visual", buttonObject.transform);
      RectTransform visualRect = visualObject.GetComponent<RectTransform>();
      SetCenteredRect(visualRect, Vector2.zero, new Vector2(28f, 28f));
      UIRoundedPanelGraphic visual = visualObject.AddComponent<UIRoundedPanelGraphic>();
      visual.Configure(new Color32(32, 38, 46, 255), Color.clear, 0f, 8f);
      visual.raycastTarget = false;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = visual;
      button.navigation = new Navigation { mode = Navigation.Mode.None };
      ColorBlock colors = button.colors;
      colors.normalColor = Color.white;
      colors.highlightedColor = new Color32(216, 255, 249, 255);
      colors.pressedColor = new Color32(224, 224, 224, 255);
      colors.selectedColor = Color.white;
      colors.disabledColor = new Color32(100, 103, 112, 170);
      colors.colorMultiplier = 1f;
      colors.fadeDuration = 0.12f;
      button.colors = colors;

      GameObject iconObject = CreateUIObject("Icon", visualObject.transform);
      RectTransform iconRect = iconObject.GetComponent<RectTransform>();
      SetCenteredRect(iconRect, Vector2.zero, new Vector2(10f, 10f));
      UICloseGraphic icon = iconObject.AddComponent<UICloseGraphic>();
      icon.Configure(new Color32(231, 233, 238, 255));
      return buttonObject;
    }

    private static GameObject BuildDockTab(string name, Transform parent)
    {
      GameObject buttonObject = CreateUIObject(name, parent);
      UIRoundedPanelGraphic background = buttonObject.AddComponent<UIRoundedPanelGraphic>();
      background.Configure(new Color32(32, 38, 46, 255), Color.clear, 0f, 10f);
      background.raycastTarget = true;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = background;
      button.navigation = new Navigation { mode = Navigation.Mode.None };
      ColorBlock colors = button.colors;
      colors.normalColor = Color.white;
      colors.highlightedColor = new Color32(216, 255, 249, 255);
      colors.pressedColor = new Color32(224, 224, 224, 255);
      colors.selectedColor = Color.white;
      colors.disabledColor = new Color32(100, 103, 112, 170);
      colors.colorMultiplier = 1f;
      colors.fadeDuration = 0.12f;
      button.colors = colors;

      GameObject iconObject = CreateUIObject("Icon", buttonObject.transform);
      RectTransform iconRect = iconObject.GetComponent<RectTransform>();
      SetCenteredRect(iconRect, Vector2.zero, new Vector2(24f, 18f));
      UIEdgeArrowGraphic icon = iconObject.AddComponent<UIEdgeArrowGraphic>();
      icon.Configure(new Color32(231, 233, 238, 255), left: true);
      return buttonObject;
    }

    private static TextMeshProUGUI BuildText(
      string name,
      Transform parent,
      string content,
      float fontSize,
      Color color,
      TMP_FontAsset font,
      TextAlignmentOptions alignment
    )
    {
      GameObject textObject = CreateUIObject(name, parent);
      TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
      text.text = content;
      text.font = font;
      text.fontSize = fontSize;
      text.fontStyle = FontStyles.Normal;
      text.alignment = alignment;
      text.textWrappingMode = TextWrappingModes.NoWrap;
      text.overflowMode = TextOverflowModes.Overflow;
      text.color = color;
      text.raycastTarget = false;
      return text;
    }

    internal static TMP_FontAsset EnsureTimelineFontAsset()
    {
      TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TimelineTmpFontPath);
      if (existing != null)
        return existing;

      Font source = AssetDatabase.LoadAssetAtPath<Font>(TimelineFontPath);
      if (source == null)
        throw new InvalidOperationException("The MapleStory OTF source font is missing.");

      TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
        source,
        72,
        8,
        GlyphRenderMode.SDFAA,
        1024,
        1024,
        AtlasPopulationMode.Dynamic,
        true
      );
      if (fontAsset == null)
        throw new InvalidOperationException("Failed to create the MapleStory TMP font asset.");

      fontAsset.name = "MAPLESTORY_OTF_BOLD Dynamic SDF";
      fontAsset.atlasTextures[0].name = "MAPLESTORY_OTF_BOLD Atlas";
      fontAsset.material.name = "MAPLESTORY_OTF_BOLD Material";
      AssetDatabase.CreateAsset(fontAsset, TimelineTmpFontPath);
      AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
      AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
      EditorUtility.SetDirty(fontAsset);
      AssetDatabase.SaveAssets();
      return fontAsset;
    }

    private static Canvas BuildCanvas()
    {
      GameObject canvasObject = new GameObject(
        "Canvas",
        typeof(RectTransform),
        typeof(Canvas),
        typeof(CanvasScaler),
        typeof(GraphicRaycaster)
      );
      Canvas canvas = canvasObject.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
      scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      scaler.referenceResolution = new Vector2(1920f, 1080f);
      scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
      scaler.matchWidthOrHeight = 0.5f;
      return canvas;
    }

    private static void BuildEventSystem()
    {
      GameObject eventSystemObject = new GameObject(
        "EventSystem",
        typeof(EventSystem),
        typeof(InputSystemUIInputModule)
      );
      eventSystemObject.GetComponent<EventSystem>().sendNavigationEvents = true;
    }

    private static void CleanupGeneratedSceneRoots(UnityEngine.SceneManagement.Scene scene)
    {
      string[] generatedNames = { "Canvas", "EventSystem", "ReplayTimelinePreview", "ReplayTimelinePreviewDriver" };
      foreach (GameObject root in scene.GetRootGameObjects().ToArray())
      {
        if (
          generatedNames.Contains(root.name) || root.name.StartsWith("ReplayTimelinePreview", StringComparison.Ordinal)
        )
          UnityEngine.Object.DestroyImmediate(root);
      }
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
      GameObject gameObject = new GameObject(name, typeof(RectTransform));
      if (parent != null)
        gameObject.transform.SetParent(parent, false);
      return gameObject;
    }

    private static void SetCenteredRect(RectTransform rect, Vector2 position, Vector2 size)
    {
      rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
      rect.pivot = new Vector2(0.5f, 0.5f);
      rect.anchoredPosition = position;
      rect.sizeDelta = size;
    }

    private static void EnsureFolders()
    {
      EnsureFolder("Assets", "Prefabs");
      EnsureFolder("Assets", "Fonts");
    }

    private static void EnsureFolder(string parent, string child)
    {
      string path = $"{parent}/{child}";
      if (!AssetDatabase.IsValidFolder(path))
        AssetDatabase.CreateFolder(parent, child);
    }

    private static Color Html(string html)
    {
      ColorUtility.TryParseHtmlString(html, out Color color);
      return color;
    }
  }
}
