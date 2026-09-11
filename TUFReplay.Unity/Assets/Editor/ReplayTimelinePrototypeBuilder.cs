using System;
using System.Linq;
using TMPro;
using TUFReplay.Unity.Notifications;
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

    private static readonly Color WarmWhite = Html("#FAFAFA");
    private static readonly Color SecondaryText = Html("#E5E5E5");
    private static readonly Color MutedText = Html("#A1A1A1");
    private static readonly Color PanelFill = Html("#0B0F17F2");
    private static readonly Color Accent = Html("#7CCF00");
    private static readonly Color AccentLight = Html("#BBF451");
    private static readonly Color AccentDeep = Html("#5EA500");
    private static readonly Color DarkIcon = Html("#35530E");
    private static readonly Color WarningAccent = Html("#F2B84B");
    private static readonly Color WarningAccentSoft = Html("#F2B84B24");

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

      BuildMicrophonePermissionWarningPreview(canvas.transform, timelineFont);

      foreach (Graphic graphic in connectedRoot.GetComponentsInChildren<Graphic>(true))
        graphic.SetAllDirty();
      Canvas.ForceUpdateCanvases();
      EditorSceneManager.MarkSceneDirty(scene);
      EditorSceneManager.SaveScene(scene, ScenePath);
      AssetDatabase.SaveAssets();
      Selection.activeGameObject = null;
      Debug.Log("[TUFReplay.Unity] Rebuilt the TUFHelperLite-style linear replay timeline prototype.");
    }

    private static void BuildMicrophonePermissionWarningPreview(Transform parent, TMP_FontAsset font)
    {
      const string PrefabPath = PrefabFolder + "/MicrophonePermissionWarningRuntime.prefab";
      AssetDatabase.DeleteAsset(PrefabPath);

      GameObject warningRoot = BuildMicrophonePermissionWarning(parent, font);
      warningRoot.SetActive(false);
      PrefabUtility.SaveAsPrefabAsset(warningRoot, PrefabPath);
      UnityEngine.Object.DestroyImmediate(warningRoot);

      GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
      if (prefab == null)
        throw new InvalidOperationException("Microphone permission warning prefab could not be created.");

      GameObject preview = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
      if (preview == null)
        throw new InvalidOperationException("Microphone permission warning preview could not be instantiated.");

      preview.name = "MicrophonePermissionWarningPreview";
      preview.SetActive(true);
      preview.GetComponent<CanvasGroup>().alpha = 1f;
      preview.transform.Find("ProgressTrack/ProgressFill").GetComponent<Image>().fillAmount = 1f;

      GameObject previewDriver = new GameObject(
        "MicrophonePermissionWarningPreviewDriver",
        typeof(MicrophonePermissionWarningPreviewDriver)
      );
      previewDriver.transform.SetParent(parent, false);
      previewDriver
        .GetComponent<MicrophonePermissionWarningPreviewDriver>()
        .Configure(preview.GetComponent<MicrophonePermissionWarningView>());
    }

    private static GameObject BuildMicrophonePermissionWarning(Transform parent, TMP_FontAsset font)
    {
      GameObject root = CreateUIObject("MicrophonePermissionWarningRuntime", parent);
      RectTransform rootRect = root.GetComponent<RectTransform>();
      rootRect.anchorMin = rootRect.anchorMax = new Vector2(1f, 0f);
      rootRect.pivot = new Vector2(1f, 0f);
      rootRect.anchoredPosition = new Vector2(-24f, 112f);
      rootRect.sizeDelta = new Vector2(600f, 154f);

      CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
      canvasGroup.alpha = 0f;
      canvasGroup.interactable = false;
      canvasGroup.blocksRaycasts = false;

      GameObject ambientShadowObject = CreateUIObject("AmbientShadow", root.transform);
      RectTransform ambientShadowRect = ambientShadowObject.GetComponent<RectTransform>();
      SetCenteredRect(ambientShadowRect, new Vector2(0f, -6f), new Vector2(608f, 162f));
      UIRoundedPanelGraphic ambientShadow = ambientShadowObject.AddComponent<UIRoundedPanelGraphic>();
      ambientShadow.Configure(new Color(0f, 0f, 0f, 0.24f), Color.clear, 0f, 22f);
      ambientShadow.raycastTarget = false;

      UIRoundedPanelGraphic panel = root.AddComponent<UIRoundedPanelGraphic>();
      panel.Configure(PanelFill, Html("#FFFFFF20"), 1f, 20f);
      panel.raycastTarget = true;

      GameObject warningBadge = CreateUIObject("WarningBadge", root.transform);
      RectTransform warningBadgeRect = warningBadge.GetComponent<RectTransform>();
      warningBadgeRect.anchorMin = warningBadgeRect.anchorMax = warningBadgeRect.pivot = new Vector2(0f, 1f);
      warningBadgeRect.anchoredPosition = new Vector2(22f, -20f);
      warningBadgeRect.sizeDelta = new Vector2(32f, 32f);
      UIRoundedPanelGraphic warningBadgeGraphic = warningBadge.AddComponent<UIRoundedPanelGraphic>();
      warningBadgeGraphic.Configure(WarningAccentSoft, Html("#F2B84B55"), 1f, 10f);
      warningBadgeGraphic.raycastTarget = false;

      TextMeshProUGUI warningGlyph = BuildText(
        "Glyph",
        warningBadge.transform,
        "!",
        19f,
        WarningAccent,
        font,
        TextAlignmentOptions.Center
      );
      RectTransform warningGlyphRect = warningGlyph.rectTransform;
      warningGlyphRect.anchorMin = Vector2.zero;
      warningGlyphRect.anchorMax = Vector2.one;
      warningGlyphRect.offsetMin = Vector2.zero;
      warningGlyphRect.offsetMax = new Vector2(0f, 1f);

      TextMeshProUGUI title = BuildText(
        "TitleText",
        root.transform,
        MicrophonePermissionWarningView.DefaultTitle,
        22f,
        WarmWhite,
        font,
        TextAlignmentOptions.MidlineLeft
      );
      RectTransform titleRect = title.rectTransform;
      titleRect.anchorMin = titleRect.anchorMax = titleRect.pivot = new Vector2(0f, 1f);
      titleRect.anchoredPosition = new Vector2(66f, -20f);
      titleRect.sizeDelta = new Vector2(470f, 32f);

      TextMeshProUGUI message = BuildText(
        "MessageText",
        root.transform,
        MicrophonePermissionWarningView.DefaultMessage,
        16f,
        MutedText,
        font,
        TextAlignmentOptions.TopLeft
      );
      RectTransform messageRect = message.rectTransform;
      messageRect.anchorMin = messageRect.anchorMax = messageRect.pivot = new Vector2(0f, 1f);
      messageRect.anchoredPosition = new Vector2(22f, -63f);
      messageRect.sizeDelta = new Vector2(556f, 64f);
      message.textWrappingMode = TextWrappingModes.Normal;
      message.overflowMode = TextOverflowModes.Ellipsis;

      GameObject dismissObject = CreateUIObject("DismissButton", root.transform);
      RectTransform dismissRect = dismissObject.GetComponent<RectTransform>();
      dismissRect.anchorMin = dismissRect.anchorMax = dismissRect.pivot = new Vector2(1f, 1f);
      dismissRect.anchoredPosition = new Vector2(-10f, -10f);
      dismissRect.sizeDelta = new Vector2(36f, 36f);
      UIRoundedPanelGraphic dismissBackground = dismissObject.AddComponent<UIRoundedPanelGraphic>();
      dismissBackground.Configure(Color.white, Color.clear, 0f, 11f);
      dismissBackground.raycastTarget = true;
      Button dismissButton = dismissObject.AddComponent<Button>();
      dismissButton.targetGraphic = dismissBackground;
      dismissButton.navigation = new Navigation { mode = Navigation.Mode.None };
      ColorBlock dismissColors = dismissButton.colors;
      dismissColors.normalColor = new Color(1f, 1f, 1f, 0.035f);
      dismissColors.highlightedColor = new Color(1f, 1f, 1f, 0.10f);
      dismissColors.pressedColor = new Color(1f, 1f, 1f, 0.16f);
      dismissColors.selectedColor = dismissColors.normalColor;
      dismissColors.disabledColor = new Color(1f, 1f, 1f, 0.02f);
      dismissColors.colorMultiplier = 1f;
      dismissColors.fadeDuration = 0.08f;
      dismissButton.colors = dismissColors;

      GameObject closeIconObject = CreateUIObject("Icon", dismissObject.transform);
      RectTransform closeIconRect = closeIconObject.GetComponent<RectTransform>();
      SetCenteredRect(closeIconRect, Vector2.zero, new Vector2(14f, 14f));
      UICloseGraphic closeIcon = closeIconObject.AddComponent<UICloseGraphic>();
      closeIcon.Configure(new Color32(168, 172, 182, 235), 1.8f, 4f);

      GameObject progressTrackObject = CreateUIObject("ProgressTrack", root.transform);
      RectTransform progressTrackRect = progressTrackObject.GetComponent<RectTransform>();
      progressTrackRect.anchorMin = new Vector2(0f, 0f);
      progressTrackRect.anchorMax = new Vector2(1f, 0f);
      progressTrackRect.pivot = new Vector2(0.5f, 0f);
      progressTrackRect.anchoredPosition = new Vector2(0f, 11f);
      progressTrackRect.sizeDelta = new Vector2(-36f, 3f);
      Image progressTrack = progressTrackObject.AddComponent<Image>();
      progressTrack.color = new Color(1f, 1f, 1f, 0.075f);
      progressTrack.raycastTarget = false;

      GameObject progressFillObject = CreateUIObject("ProgressFill", progressTrackObject.transform);
      RectTransform progressFillRect = progressFillObject.GetComponent<RectTransform>();
      progressFillRect.anchorMin = Vector2.zero;
      progressFillRect.anchorMax = Vector2.one;
      progressFillRect.offsetMin = Vector2.zero;
      progressFillRect.offsetMax = Vector2.zero;
      Image progressFill = progressFillObject.AddComponent<Image>();
      progressFill.color = WarningAccent;
      progressFill.type = Image.Type.Filled;
      progressFill.fillMethod = Image.FillMethod.Horizontal;
      progressFill.fillOrigin = 0;
      progressFill.fillAmount = 1f;
      progressFill.raycastTarget = false;

      MicrophonePermissionWarningView view = root.AddComponent<MicrophonePermissionWarningView>();
      view.ConfigureReferences(canvasGroup, title, message, progressFill, dismissButton);
      return root;
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
      floatingPanelRect.anchoredPosition = new Vector2(0f, -382f);
      floatingPanelRect.sizeDelta = new Vector2(432f, 132f);

      GameObject mainPanel = CreateUIObject("MainPanel", floatingPanel.transform);
      RectTransform mainPanelRect = mainPanel.GetComponent<RectTransform>();
      SetCenteredRect(mainPanelRect, Vector2.zero, new Vector2(432f, 132f));

      GameObject ambientShadowObject = CreateUIObject("AmbientShadow", mainPanel.transform);
      RectTransform ambientShadowRect = ambientShadowObject.GetComponent<RectTransform>();
      SetCenteredRect(ambientShadowRect, new Vector2(0f, -7f), new Vector2(438f, 138f));
      UIRoundedPanelGraphic ambientShadow = ambientShadowObject.AddComponent<UIRoundedPanelGraphic>();
      ambientShadow.Configure(new Color(0f, 0f, 0f, 0.08f), Color.clear, 0f, 22f);
      ambientShadow.raycastTarget = false;

      GameObject shadowObject = CreateUIObject("PanelShadow", mainPanel.transform);
      RectTransform shadowRect = shadowObject.GetComponent<RectTransform>();
      shadowRect.anchorMin = shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
      shadowRect.sizeDelta = new Vector2(432f, 132f);
      shadowRect.anchoredPosition = new Vector2(0f, -4f);
      UIRoundedPanelGraphic shadow = shadowObject.AddComponent<UIRoundedPanelGraphic>();
      shadow.Configure(new Color(0f, 0f, 0f, 0.16f), Color.clear, 0f, 20f);
      shadow.raycastTarget = false;

      GameObject panelObject = CreateUIObject("PanelBackground", mainPanel.transform);
      RectTransform panelRect = panelObject.GetComponent<RectTransform>();
      panelRect.anchorMin = Vector2.zero;
      panelRect.anchorMax = Vector2.one;
      panelRect.offsetMin = Vector2.zero;
      panelRect.offsetMax = Vector2.zero;
      UIRoundedPanelGraphic panel = panelObject.AddComponent<UIRoundedPanelGraphic>();
      panel.Configure(PanelFill, Html("#FFFFFF14"), 0.75f, 20f);
      panel.raycastTarget = true;

      GameObject topHighlightObject = CreateUIObject("TopHighlight", mainPanel.transform);
      RectTransform topHighlightRect = topHighlightObject.GetComponent<RectTransform>();
      SetCenteredRect(topHighlightRect, new Vector2(0f, 63f), new Vector2(388f, 1f));
      UIRoundedPanelGraphic topHighlight = topHighlightObject.AddComponent<UIRoundedPanelGraphic>();
      topHighlight.Configure(Html("#FFFFFF1C"), Color.clear, 0f, 0.5f);
      topHighlight.raycastTarget = false;

      TextMeshProUGUI elapsedText = BuildText(
        "ElapsedTime",
        mainPanel.transform,
        "01:24",
        14f,
        WarmWhite,
        timelineFont,
        TextAlignmentOptions.Left
      );
      SetCenteredRect(elapsedText.rectTransform, new Vector2(-166f, 43f), new Vector2(54f, 20f));

      GameObject playbackStatusObject = CreateUIObject("PlaybackStatus", mainPanel.transform);
      RectTransform playbackStatusRect = playbackStatusObject.GetComponent<RectTransform>();
      SetCenteredRect(playbackStatusRect, new Vector2(0f, 43f), new Vector2(86f, 20f));

      GameObject statusDotObject = CreateUIObject("StatusDot", playbackStatusObject.transform);
      RectTransform statusDotRect = statusDotObject.GetComponent<RectTransform>();
      SetCenteredRect(statusDotRect, new Vector2(-31f, 0f), new Vector2(6f, 6f));
      UIFilledCircleGraphic statusDot = statusDotObject.AddComponent<UIFilledCircleGraphic>();
      statusDot.color = Accent;
      statusDot.raycastTarget = false;

      TextMeshProUGUI statusText = BuildText(
        "StatusLabel",
        playbackStatusObject.transform,
        "PLAYING",
        10f,
        SecondaryText,
        timelineFont,
        TextAlignmentOptions.Left
      );
      SetCenteredRect(statusText.rectTransform, new Vector2(9f, 0f), new Vector2(62f, 18f));

      TextMeshProUGUI durationText = BuildText(
        "DurationTime",
        mainPanel.transform,
        "03:48",
        14f,
        MutedText,
        timelineFont,
        TextAlignmentOptions.Right
      );
      SetCenteredRect(durationText.rectTransform, new Vector2(166f, 43f), new Vector2(54f, 20f));

      GameObject timelineHitObject = CreateUIObject("TimelineHitArea", mainPanel.transform);
      RectTransform timelineHitRect = timelineHitObject.GetComponent<RectTransform>();
      SetCenteredRect(timelineHitRect, new Vector2(0f, 13f), new Vector2(388f, 28f));
      Image timelineHitImage = timelineHitObject.AddComponent<Image>();
      timelineHitImage.color = Color.clear;
      timelineHitImage.raycastTarget = true;
      UITimelineSeekInput seekInput = timelineHitObject.AddComponent<UITimelineSeekInput>();
      seekInput.Configure(timelineHitRect);

      GameObject markerObject = CreateUIObject("JudgmentMarkers", timelineHitObject.transform);
      RectTransform markerRect = markerObject.GetComponent<RectTransform>();
      SetCenteredRect(markerRect, new Vector2(0f, 1f), new Vector2(388f, 26f));
      UIJudgmentMarkerGraphic judgmentMarkers = markerObject.AddComponent<UIJudgmentMarkerGraphic>();
      judgmentMarkers.Configure(2f, 8f, 8f);

      GameObject trackObject = CreateUIObject("LinearTimeline", timelineHitObject.transform);
      RectTransform trackRect = trackObject.GetComponent<RectTransform>();
      SetCenteredRect(trackRect, new Vector2(0f, -1f), new Vector2(388f, 10f));
      UILinearTimelineGraphic track = trackObject.AddComponent<UILinearTimelineGraphic>();
      track.Configure(Html("#FFFFFF14"), AccentLight, Accent, AccentDeep);
      track.raycastTarget = false;

      GameObject playheadObject = CreateUIObject("Playhead", trackObject.transform);
      RectTransform playheadRect = playheadObject.GetComponent<RectTransform>();
      playheadRect.anchorMin = playheadRect.anchorMax = new Vector2(84f / 228f, 0.5f);
      playheadRect.sizeDelta = new Vector2(16f, 16f);
      UIFilledCircleGraphic playheadGlow = playheadObject.AddComponent<UIFilledCircleGraphic>();
      playheadGlow.color = new Color(Accent.r, Accent.g, Accent.b, 0.28f);
      playheadGlow.raycastTarget = false;

      GameObject playheadCoreObject = CreateUIObject("Core", playheadObject.transform);
      RectTransform playheadCoreRect = playheadCoreObject.GetComponent<RectTransform>();
      SetCenteredRect(playheadCoreRect, Vector2.zero, new Vector2(10f, 10f));
      UIFilledCircleGraphic playhead = playheadCoreObject.AddComponent<UIFilledCircleGraphic>();
      playhead.color = WarmWhite;
      playhead.raycastTarget = false;

      GameObject backwardObject = BuildSeekButton("SeekBackward", mainPanel.transform, "−5s", timelineFont);
      RectTransform backwardRect = backwardObject.GetComponent<RectTransform>();
      SetCenteredRect(backwardRect, new Vector2(-170f, -32f), new Vector2(40f, 30f));

      GameObject playButton = PrefabUtility.InstantiatePrefab(playButtonPrefab, mainPanel.transform) as GameObject;
      RectTransform playRect = playButton.GetComponent<RectTransform>();
      SetCenteredRect(playRect, new Vector2(-122f, -32f), new Vector2(36f, 36f));

      GameObject forwardObject = BuildSeekButton("SeekForward", mainPanel.transform, "+5s", timelineFont);
      RectTransform forwardRect = forwardObject.GetComponent<RectTransform>();
      SetCenteredRect(forwardRect, new Vector2(-74f, -32f), new Vector2(40f, 30f));

      GameObject judgmentButtonObject = BuildJudgmentButton(mainPanel.transform, timelineFont);
      RectTransform judgmentButtonRect = judgmentButtonObject.GetComponent<RectTransform>();
      SetCenteredRect(judgmentButtonRect, new Vector2(74f, -32f), new Vector2(140f, 30f));

      GameObject dockButtonObject = BuildDockButton("DockButton", mainPanel.transform);
      RectTransform dockButtonRect = dockButtonObject.GetComponent<RectTransform>();
      SetCenteredRect(dockButtonRect, new Vector2(190f, -32f), new Vector2(30f, 30f));

      UIFloatingPanelDragHandle dragHandle = floatingPanel.AddComponent<UIFloatingPanelDragHandle>();
      dragHandle.Configure(
        floatingPanelRect,
        new[] { backwardRect, playRect, forwardRect, timelineHitRect, judgmentButtonRect, dockButtonRect },
        16f
      );

      GameObject dockTabProximityObject = CreateUIObject("DockTabProximityArea", root.transform);
      RectTransform dockTabProximityRect = dockTabProximityObject.GetComponent<RectTransform>();
      SetCenteredRect(dockTabProximityRect, new Vector2(982f, -382f), new Vector2(28f, 96f));
      UIRoundedPanelGraphic dockTabProximityGraphic = dockTabProximityObject.AddComponent<UIRoundedPanelGraphic>();
      dockTabProximityGraphic.Configure(Color.clear, Color.clear, 0f, 0f);
      dockTabProximityGraphic.raycastTarget = true;
      UIDockTabProximityTrigger dockTabProximityTrigger =
        dockTabProximityObject.AddComponent<UIDockTabProximityTrigger>();

      GameObject dockTabObject = BuildDockTab("DockTab", root.transform);
      RectTransform dockTabRect = dockTabObject.GetComponent<RectTransform>();
      SetCenteredRect(dockTabRect, new Vector2(982f, -382f), new Vector2(36f, 42f));
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
        dockTabObject.GetComponent<Button>(),
        dockTabProximityRect,
        dockTabProximityTrigger,
        dockTabObject.GetComponent<UIDockTabProximityTrigger>()
      );

      UIJudgmentFilterDropdown judgmentFilter = BuildJudgmentDropdown(
        root.transform,
        rootRect,
        floatingPanelRect,
        judgmentButtonObject,
        judgmentMarkers,
        timelineFont
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
        dockController,
        judgmentFilter
      );
      return root;
    }

    private static GameObject BuildJudgmentButton(Transform parent, TMP_FontAsset font)
    {
      GameObject buttonObject = CreateUIObject("JudgmentFilterButton", parent);
      UIRoundedPanelGraphic background = buttonObject.AddComponent<UIRoundedPanelGraphic>();
      background.Configure(Color.white, Color.clear, 0f, 9f);
      background.raycastTarget = true;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = background;
      button.navigation = new Navigation { mode = Navigation.Mode.None };
      ColorBlock colors = button.colors;
      colors.normalColor = new Color(1f, 1f, 1f, 0.035f);
      colors.highlightedColor = new Color(0.92f, 1f, 0.78f, 0.075f);
      colors.pressedColor = new Color(0.75f, 0.96f, 0.52f, 0.12f);
      colors.selectedColor = colors.normalColor;
      colors.disabledColor = new Color(1f, 1f, 1f, 0.02f);
      colors.colorMultiplier = 1f;
      colors.fadeDuration = 0.12f;
      button.colors = colors;

      TextMeshProUGUI label = BuildText(
        "Label",
        buttonObject.transform,
        "Judgements",
        10f,
        SecondaryText,
        font,
        TextAlignmentOptions.Left
      );
      label.rectTransform.anchorMin = Vector2.zero;
      label.rectTransform.anchorMax = Vector2.one;
      label.rectTransform.offsetMin = new Vector2(12f, 0f);
      label.rectTransform.offsetMax = new Vector2(-56f, 0f);

      GameObject countBadgeObject = CreateUIObject("CountBadge", buttonObject.transform);
      RectTransform countBadgeRect = countBadgeObject.GetComponent<RectTransform>();
      SetCenteredRect(countBadgeRect, new Vector2(31f, 0f), new Vector2(20f, 16f));
      UIRoundedPanelGraphic countBadge = countBadgeObject.AddComponent<UIRoundedPanelGraphic>();
      countBadge.Configure(Html("#7CCF001A"), Color.clear, 0f, 8f);
      countBadge.raycastTarget = false;

      TextMeshProUGUI countLabel = BuildText(
        "Count",
        countBadgeObject.transform,
        "0",
        10f,
        Accent,
        font,
        TextAlignmentOptions.Center
      );
      countLabel.rectTransform.anchorMin = Vector2.zero;
      countLabel.rectTransform.anchorMax = Vector2.one;
      countLabel.rectTransform.offsetMin = Vector2.zero;
      countLabel.rectTransform.offsetMax = Vector2.zero;

      GameObject chevronObject = CreateUIObject("Chevron", buttonObject.transform);
      RectTransform chevronRect = chevronObject.GetComponent<RectTransform>();
      SetCenteredRect(chevronRect, new Vector2(57f, 0f), new Vector2(16f, 16f));
      UILucideChevronGraphic chevron = chevronObject.AddComponent<UILucideChevronGraphic>();
      chevron.Configure(MutedText, 2.1f);
      return buttonObject;
    }

    private static UIJudgmentFilterDropdown BuildJudgmentDropdown(
      Transform parent,
      RectTransform canvasRoot,
      RectTransform floatingPanel,
      GameObject judgmentButton,
      UIJudgmentMarkerGraphic markerGraphic,
      TMP_FontAsset font
    )
    {
      GameObject blockerObject = CreateUIObject("JudgmentDropdownBlocker", parent);
      RectTransform blockerRect = blockerObject.GetComponent<RectTransform>();
      blockerRect.anchorMin = Vector2.zero;
      blockerRect.anchorMax = Vector2.one;
      blockerRect.offsetMin = Vector2.zero;
      blockerRect.offsetMax = Vector2.zero;
      Image blockerImage = blockerObject.AddComponent<Image>();
      blockerImage.color = Color.clear;
      blockerImage.raycastTarget = true;
      Button blockerButton = blockerObject.AddComponent<Button>();
      blockerButton.targetGraphic = blockerImage;
      blockerButton.transition = Selectable.Transition.None;

      GameObject popupObject = CreateUIObject("JudgmentDropdown", parent);
      RectTransform popupRect = popupObject.GetComponent<RectTransform>();
      SetCenteredRect(popupRect, Vector2.zero, new Vector2(432f, 140f));

      GameObject shadowObject = CreateUIObject("Shadow", popupObject.transform);
      RectTransform shadowRect = shadowObject.GetComponent<RectTransform>();
      shadowRect.anchorMin = Vector2.zero;
      shadowRect.anchorMax = Vector2.one;
      shadowRect.offsetMin = new Vector2(-2f, -7f);
      shadowRect.offsetMax = new Vector2(2f, -3f);
      UIRoundedPanelGraphic shadow = shadowObject.AddComponent<UIRoundedPanelGraphic>();
      shadow.Configure(new Color(0f, 0f, 0f, 0.32f), Color.clear, 0f, 16f);
      shadow.raycastTarget = false;

      GameObject backgroundObject = CreateUIObject("Background", popupObject.transform);
      RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
      backgroundRect.anchorMin = Vector2.zero;
      backgroundRect.anchorMax = Vector2.one;
      backgroundRect.offsetMin = Vector2.zero;
      backgroundRect.offsetMax = Vector2.zero;
      UIRoundedPanelGraphic background = backgroundObject.AddComponent<UIRoundedPanelGraphic>();
      background.Configure(Html("#0B0F17FA"), Html("#FFFFFF20"), 1f, 16f);
      background.raycastTarget = true;

      TextMeshProUGUI popupTitle = BuildText(
        "Title",
        popupObject.transform,
        "JUDGEMENT MARKERS",
        10f,
        MutedText,
        font,
        TextAlignmentOptions.Left
      );
      SetCenteredRect(popupTitle.rectTransform, new Vector2(0f, 55f), new Vector2(396f, 18f));

      ReplayJudgmentKind[] categories =
      {
        ReplayJudgmentKind.EarlyPerfect,
        ReplayJudgmentKind.Perfect,
        ReplayJudgmentKind.LatePerfect,
        ReplayJudgmentKind.TooEarly,
        ReplayJudgmentKind.Early,
        ReplayJudgmentKind.Late,
        ReplayJudgmentKind.Miss,
        ReplayJudgmentKind.Overload,
      };
      string[] labels =
      {
        "Early Perfect:",
        "Perfect:",
        "Late Perfect:",
        "Too Early:",
        "Early:",
        "Late:",
        "Miss:",
        "Overload:",
      };
      Vector2[] optionPositions =
      {
        new Vector2(-142f, 24f),
        new Vector2(0f, 24f),
        new Vector2(142f, 24f),
        new Vector2(-118f, -6f),
        new Vector2(0f, -6f),
        new Vector2(118f, -6f),
        new Vector2(-64f, -36f),
        new Vector2(64f, -36f),
      };
      Toggle[] toggles = new Toggle[categories.Length];
      CanvasGroup[] optionVisuals = new CanvasGroup[categories.Length];
      TMP_Text[] optionCountLabels = new TMP_Text[categories.Length];
      for (int index = 0; index < categories.Length; index++)
      {
        GameObject rowObject = CreateUIObject("Option_" + categories[index], popupObject.transform);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        SetCenteredRect(rowRect, optionPositions[index], new Vector2(132f, 26f));
        CanvasGroup optionVisual = rowObject.AddComponent<CanvasGroup>();

        Image rowHitArea = rowObject.AddComponent<Image>();
        rowHitArea.color = Color.clear;
        rowHitArea.raycastTarget = true;

        Toggle toggle = rowObject.AddComponent<Toggle>();
        toggle.navigation = new Navigation { mode = Navigation.Mode.None };
        toggle.isOn = false;

        GameObject boxObject = CreateUIObject("Checkbox", rowObject.transform);
        RectTransform boxRect = boxObject.GetComponent<RectTransform>();
        UIRoundedPanelGraphic box = boxObject.AddComponent<UIRoundedPanelGraphic>();
        box.Configure(Html("#FFFFFF08"), Html("#FFFFFF2E"), 1f, 8f);
        box.raycastTarget = true;

        GameObject selectedObject = CreateUIObject("Selected", boxObject.transform);
        RectTransform selectedRect = selectedObject.GetComponent<RectTransform>();
        SetCenteredRect(selectedRect, Vector2.zero, new Vector2(8f, 8f));
        UIRoundedPanelGraphic selected = selectedObject.AddComponent<UIRoundedPanelGraphic>();
        selected.Configure(ReplayJudgmentPalette.GetColor(categories[index]), Color.clear, 0f, 4f);
        selected.raycastTarget = false;

        TextMeshProUGUI label = BuildText(
          "Label",
          rowObject.transform,
          labels[index],
          11f,
          SecondaryText,
          font,
          TextAlignmentOptions.Left
        );
        label.ForceMeshUpdate();
        float labelWidth = Mathf.Ceil(label.preferredWidth);

        TextMeshProUGUI count = BuildText(
          "Count",
          rowObject.transform,
          "0",
          11f,
          ReplayJudgmentPalette.GetColor(categories[index]),
          font,
          TextAlignmentOptions.Left
        );
        const float countWidth = 14f;
        float contentWidth = 16f + 7f + labelWidth + 5f + countWidth;
        float contentLeft = contentWidth * -0.5f;
        SetCenteredRect(boxRect, new Vector2(contentLeft + 8f, 0f), new Vector2(16f, 16f));
        SetCenteredRect(
          label.rectTransform,
          new Vector2(contentLeft + 23f + labelWidth * 0.5f, 0f),
          new Vector2(labelWidth, 20f)
        );
        SetCenteredRect(
          count.rectTransform,
          new Vector2(contentLeft + 28f + labelWidth + countWidth * 0.5f, 0f),
          new Vector2(countWidth, 20f)
        );

        toggle.targetGraphic = rowHitArea;
        toggle.graphic = selected;
        ColorBlock toggleColors = toggle.colors;
        toggleColors.normalColor = Color.clear;
        toggleColors.highlightedColor = new Color(1f, 1f, 1f, 0.07f);
        toggleColors.pressedColor = new Color(1f, 1f, 1f, 0.12f);
        toggleColors.selectedColor = Color.clear;
        toggleColors.disabledColor = Color.clear;
        toggleColors.fadeDuration = 0.1f;
        toggle.colors = toggleColors;
        toggles[index] = toggle;
        optionVisuals[index] = optionVisual;
        optionCountLabels[index] = count;
      }

      UIJudgmentFilterDropdown controller = parent.gameObject.AddComponent<UIJudgmentFilterDropdown>();
      controller.Configure(
        canvasRoot,
        floatingPanel,
        popupRect,
        blockerObject,
        judgmentButton.GetComponent<Button>(),
        blockerButton,
        judgmentButton.transform.Find("Label").GetComponent<TMP_Text>(),
        judgmentButton.transform.Find("CountBadge/Count").GetComponent<TMP_Text>(),
        judgmentButton.transform.Find("Chevron") as RectTransform,
        toggles,
        optionVisuals,
        optionCountLabels,
        categories,
        markerGraphic
      );
      blockerObject.SetActive(false);
      popupObject.SetActive(false);
      return controller;
    }

    private static GameObject BuildPlayButtonPrefab()
    {
      GameObject buttonObject = CreateUIObject("PlayPause", null);
      buttonObject.GetComponent<RectTransform>().sizeDelta = new Vector2(36f, 36f);
      UIPlayPauseGraphic graphic = buttonObject.AddComponent<UIPlayPauseGraphic>();
      graphic.Configure(Accent, DarkIcon);
      graphic.raycastTarget = true;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = graphic;
      button.navigation = new Navigation { mode = Navigation.Mode.None };
      ColorBlock colors = button.colors;
      colors.normalColor = Color.white;
      colors.highlightedColor = Html("#E8FFD0");
      colors.pressedColor = Html("#C8F58B");
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
      background.Configure(Color.white, Color.clear, 0f, 8f);
      background.raycastTarget = true;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = background;
      button.navigation = new Navigation { mode = Navigation.Mode.None };
      ColorBlock colors = button.colors;
      colors.normalColor = new Color(1f, 1f, 1f, 0.025f);
      colors.highlightedColor = new Color(0.92f, 1f, 0.78f, 0.075f);
      colors.pressedColor = new Color(0.75f, 0.96f, 0.52f, 0.12f);
      colors.selectedColor = colors.normalColor;
      colors.disabledColor = new Color(1f, 1f, 1f, 0.015f);
      colors.colorMultiplier = 1f;
      colors.fadeDuration = 0.12f;
      button.colors = colors;

      TextMeshProUGUI text = BuildText(
        "Label",
        buttonObject.transform,
        label,
        10f,
        SecondaryText,
        font,
        TextAlignmentOptions.Center
      );
      text.rectTransform.anchorMin = Vector2.zero;
      text.rectTransform.anchorMax = Vector2.one;
      text.rectTransform.offsetMin = Vector2.zero;
      text.rectTransform.offsetMax = Vector2.zero;
      return buttonObject;
    }

    private static GameObject BuildDockButton(string name, Transform parent)
    {
      GameObject buttonObject = CreateUIObject(name, parent);
      UIRoundedPanelGraphic hitArea = buttonObject.AddComponent<UIRoundedPanelGraphic>();
      hitArea.Configure(Color.clear, Color.clear, 0f, 0f);
      hitArea.raycastTarget = true;

      GameObject visualObject = CreateUIObject("Visual", buttonObject.transform);
      RectTransform visualRect = visualObject.GetComponent<RectTransform>();
      SetCenteredRect(visualRect, Vector2.zero, new Vector2(30f, 30f));
      UIRoundedPanelGraphic visual = visualObject.AddComponent<UIRoundedPanelGraphic>();
      visual.Configure(Color.white, Color.clear, 0f, 15f);
      visual.raycastTarget = false;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = visual;
      button.navigation = new Navigation { mode = Navigation.Mode.None };
      ColorBlock colors = button.colors;
      colors.normalColor = Color.clear;
      colors.highlightedColor = new Color(1f, 1f, 1f, 0.075f);
      colors.pressedColor = new Color(0.75f, 0.96f, 0.52f, 0.12f);
      colors.selectedColor = Color.clear;
      colors.disabledColor = Color.clear;
      colors.colorMultiplier = 1f;
      colors.fadeDuration = 0.12f;
      button.colors = colors;

      GameObject iconObject = CreateUIObject("Icon", visualObject.transform);
      RectTransform iconRect = iconObject.GetComponent<RectTransform>();
      SetCenteredRect(iconRect, Vector2.zero, new Vector2(10f, 10f));
      UICloseGraphic icon = iconObject.AddComponent<UICloseGraphic>();
      icon.Configure(new Color32(161, 161, 161, 220), 1.8f, 3.2f);
      return buttonObject;
    }

    private static GameObject BuildDockTab(string name, Transform parent)
    {
      GameObject buttonObject = CreateUIObject(name, parent);
      UIRoundedPanelGraphic background = buttonObject.AddComponent<UIRoundedPanelGraphic>();
      background.Configure(PanelFill, Html("#FFFFFF20"), 1f, 14f);
      background.ConfigureCorners(topLeft: true, topRight: false, bottomRight: false, bottomLeft: true);
      background.raycastTarget = true;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = background;
      button.navigation = new Navigation { mode = Navigation.Mode.None };
      ColorBlock colors = button.colors;
      colors.normalColor = Color.white;
      colors.highlightedColor = Html("#E8FFD0");
      colors.pressedColor = Html("#C8F58B");
      colors.selectedColor = Color.white;
      colors.disabledColor = new Color32(100, 103, 112, 170);
      colors.colorMultiplier = 1f;
      colors.fadeDuration = 0.12f;
      button.colors = colors;

      GameObject iconObject = CreateUIObject("Icon", buttonObject.transform);
      RectTransform iconRect = iconObject.GetComponent<RectTransform>();
      SetCenteredRect(iconRect, Vector2.zero, new Vector2(18f, 18f));
      iconRect.localRotation = Quaternion.Euler(0f, 0f, -90f);
      UILucideChevronGraphic icon = iconObject.AddComponent<UILucideChevronGraphic>();
      icon.Configure(MutedText, 2.1f);
      buttonObject.AddComponent<UIDockTabProximityTrigger>();
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
