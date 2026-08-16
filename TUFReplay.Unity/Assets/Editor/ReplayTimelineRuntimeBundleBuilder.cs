using System;
using System.IO;
using System.Linq;
using TMPro;
using TUFReplay.Unity.ReplayTimeline;
using UnityEditor;
using UnityEngine;

namespace TUFReplay.Unity.Editor
{
  public static class ReplayTimelineRuntimeBundleBuilder
  {
    private const string PreviewPrefabPath = "Assets/Prefabs/ReplayTimeline.prefab";
    private const string RuntimePrefabPath = "Assets/Prefabs/ReplayTimelineRuntime.prefab";
    private const string TimelineFontPath = "Assets/Fonts/MAPLESTORY_OTF_BOLD.OTF";
    private const string TimelineTmpFontPath = "Assets/Fonts/MAPLESTORY_OTF_BOLD Dynamic SDF.asset";
    private const string BundleName = "tufreplay_ui.bundle";

    [MenuItem("Tools/TUFReplay/Build Runtime UI Bundles")]
    public static void BuildRuntimeUiBundles()
    {
      ReplayTimelinePrototypeBuilder.Rebuild();
      BuildRuntimePrefab();
      ValidateRuntimePrefab();

      BuildBundle(BuildTarget.StandaloneOSX, "mac");
      BuildBundle(BuildTarget.StandaloneWindows64, "win");
      BuildBundle(BuildTarget.StandaloneLinux64, "linux");
      AssetDatabase.Refresh();
      Debug.Log("[TUFReplay.Unity] Built and validated macOS, Windows, and Linux replay timeline bundles.");
    }

    private static void BuildRuntimePrefab()
    {
      AssetDatabase.ImportAsset(
        PreviewPrefabPath,
        ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate
      );
      GameObject previewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPrefabPath);
      if (previewPrefab == null)
        throw new InvalidOperationException("Replay timeline preview prefab is missing.");

      GameObject instance = PrefabUtility.InstantiatePrefab(previewPrefab) as GameObject;
      if (instance == null)
        throw new InvalidOperationException("Replay timeline preview prefab could not be instantiated.");

      try
      {
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = "ReplayTimelineRuntime";
        ReplayTimelinePreviewDriver preview = instance.GetComponentInChildren<ReplayTimelinePreviewDriver>(true);
        if (preview != null)
          UnityEngine.Object.DestroyImmediate(preview);

        AssetDatabase.DeleteAsset(RuntimePrefabPath);
        PrefabUtility.SaveAsPrefabAsset(instance, RuntimePrefabPath);
      }
      finally
      {
        UnityEngine.Object.DestroyImmediate(instance);
      }
      AssetDatabase.SaveAssets();
      AssetDatabase.ImportAsset(
        RuntimePrefabPath,
        ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate
      );
    }

    private static void ValidateRuntimePrefab()
    {
      string[] dependencies = AssetDatabase.GetDependencies(RuntimePrefabPath, true);
      string forbidden = dependencies.FirstOrDefault(path =>
        path.StartsWith("Assets/Art/ADOFAI/", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith("OrbitingPlanetPairPreview.cs", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith("UIOrbitTrackGraphic.cs", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith("UIPlanetTrailGraphic.cs", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith("ADOFAIPlanetFrames.shader", StringComparison.OrdinalIgnoreCase)
      );
      if (forbidden != null)
        throw new InvalidOperationException("Runtime timeline contains a forbidden legacy dependency: " + forbidden);
      if (!dependencies.Contains(TimelineFontPath, StringComparer.OrdinalIgnoreCase))
        throw new InvalidOperationException("Runtime timeline does not contain the MapleStory OTF source font.");
      if (!dependencies.Contains(TimelineTmpFontPath, StringComparer.OrdinalIgnoreCase))
        throw new InvalidOperationException("Runtime timeline does not contain the MapleStory TMP SDF font.");

      GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimePrefabPath);
      if (prefab == null || prefab.GetComponent<ReplayTimelineView>() == null)
        throw new InvalidOperationException("Runtime timeline view is invalid.");
      if (prefab.GetComponentInChildren<ReplayTimelinePreviewDriver>(true) != null)
        throw new InvalidOperationException("Runtime timeline contains the preview driver.");
      if (prefab.GetComponentInChildren<UILinearTimelineGraphic>(true) == null)
        throw new InvalidOperationException("Runtime timeline does not contain the linear timeline graphic.");
      if (prefab.GetComponentInChildren<UITimelineSeekInput>(true) == null)
        throw new InvalidOperationException("Runtime timeline does not contain timeline seek input.");
      if (prefab.GetComponent<UIFloatingPanelDockController>() == null)
        throw new InvalidOperationException("Runtime timeline does not contain the floating panel dock controller.");
      if (prefab.GetComponentInChildren<UIJudgmentMarkerGraphic>(true) == null)
        throw new InvalidOperationException("Runtime timeline does not contain the judgment marker graphic.");
      if (prefab.GetComponentInChildren<UIJudgmentFilterDropdown>(true) == null)
        throw new InvalidOperationException("Runtime timeline does not contain the judgment filter dropdown.");

      RectTransform panel = FindRect(prefab.transform, "FloatingPanel");
      ValidateRect(panel, "floating panel", new Vector2(432f, 132f), null);
      ValidateRect(FindRect(panel, "MainPanel"), "main panel", new Vector2(432f, 132f), Vector2.zero);
      ValidateRect(FindRect(panel, "AmbientShadow"), "ambient shadow", new Vector2(438f, 138f), new Vector2(0f, -7f));
      ValidateRect(FindRect(panel, "PanelShadow"), "panel shadow", new Vector2(432f, 132f), null);
      ValidateRect(FindRect(panel, "ElapsedTime"), "elapsed time", new Vector2(54f, 20f), new Vector2(-166f, 43f));
      ValidateRect(FindRect(panel, "PlaybackStatus"), "playback status", new Vector2(86f, 20f), new Vector2(0f, 43f));
      ValidateRect(FindRect(panel, "DurationTime"), "duration time", new Vector2(54f, 20f), new Vector2(166f, 43f));
      ValidateRect(
        FindRect(panel, "TimelineHitArea"),
        "timeline hit area",
        new Vector2(388f, 28f),
        new Vector2(0f, 13f)
      );
      ValidateRect(FindRect(panel, "LinearTimeline"), "timeline track", new Vector2(388f, 10f), null);
      ValidateRect(FindRect(panel, "JudgmentMarkers"), "judgment markers", new Vector2(388f, 26f), null);
      ValidateRect(FindRect(panel, "SeekBackward"), "backward seek", new Vector2(40f, 30f), new Vector2(-170f, -32f));
      ValidateRect(FindRect(panel, "PlayButton"), "play button", new Vector2(36f, 36f), new Vector2(-122f, -32f));
      ValidateRect(FindRect(panel, "SeekForward"), "forward seek", new Vector2(40f, 30f), new Vector2(-74f, -32f));
      ValidateRect(
        FindRect(panel, "JudgmentFilterButton"),
        "judgment filter button",
        new Vector2(140f, 30f),
        new Vector2(74f, -32f)
      );
      RectTransform dockButton = FindRect(panel, "DockButton");
      ValidateRect(dockButton, "dock button", new Vector2(30f, 30f), new Vector2(190f, -32f));
      if (dockButton?.GetComponentInChildren<UICloseGraphic>(true) == null)
        throw new InvalidOperationException("Runtime timeline dock button does not contain the rounded close graphic.");
      ValidateRect(FindRect(prefab.transform, "JudgmentDropdown"), "judgment dropdown", new Vector2(272f, 196f), null);
      RectTransform dockTab = FindRect(prefab.transform, "DockTab");
      ValidateRect(dockTab, "dock tab", new Vector2(36f, 42f), null);
      RectTransform dockTabIcon = dockTab?.Find("Icon") as RectTransform;
      ValidateRect(dockTabIcon, "dock tab icon", new Vector2(18f, 18f), Vector2.zero);
      if (dockTabIcon?.GetComponent<UILucideChevronGraphic>() == null)
        throw new InvalidOperationException("Runtime timeline dock tab does not contain the Lucide chevron graphic.");
      if (Quaternion.Angle(dockTabIcon.localRotation, Quaternion.Euler(0f, 0f, -90f)) > 0.01f)
        throw new InvalidOperationException("Runtime timeline dock tab chevron does not point left.");
      if (dockTab?.GetComponent<UIDockTabProximityTrigger>() == null)
        throw new InvalidOperationException("Runtime timeline dock tab does not contain its hover trigger.");
      RectTransform dockTabProximityArea = FindRect(prefab.transform, "DockTabProximityArea");
      ValidateRect(dockTabProximityArea, "dock tab proximity area", new Vector2(28f, 96f), null);
      if (dockTabProximityArea?.GetComponent<UIDockTabProximityTrigger>() == null)
        throw new InvalidOperationException("Runtime timeline does not contain the dock tab proximity trigger.");
      ValidateTextFont(FindRect(panel, "ElapsedTime")?.GetComponent<TMP_Text>(), "elapsed time");
      ValidateTextFont(FindRect(panel, "DurationTime")?.GetComponent<TMP_Text>(), "duration time");
      ValidateTextFont(FindRect(panel, "SeekBackward")?.Find("Label")?.GetComponent<TMP_Text>(), "backward seek label");
      ValidateTextFont(FindRect(panel, "SeekForward")?.Find("Label")?.GetComponent<TMP_Text>(), "forward seek label");
      ValidateTextFont(
        FindRect(panel, "JudgmentFilterButton")?.Find("Label")?.GetComponent<TMP_Text>(),
        "judgment filter label"
      );
      ValidateTextFont(
        FindRect(panel, "JudgmentFilterButton")?.Find("CountBadge/Count")?.GetComponent<TMP_Text>(),
        "judgment filter count"
      );
      ValidateRuntimeInstantiation(prefab);
    }

    private static RectTransform FindRect(Transform root, string name)
    {
      return root
        ?.GetComponentsInChildren<RectTransform>(true)
        .FirstOrDefault(candidate => string.Equals(candidate.name, name, StringComparison.Ordinal));
    }

    private static void ValidateRect(RectTransform rect, string label, Vector2 expectedSize, Vector2? expectedPosition)
    {
      if (rect == null || (rect.sizeDelta - expectedSize).sqrMagnitude > 0.001f)
        throw new InvalidOperationException(
          $"Runtime timeline {label} has size {rect?.sizeDelta.ToString() ?? "<missing>"}; expected {expectedSize}."
        );
      if (expectedPosition.HasValue && (rect.anchoredPosition - expectedPosition.Value).sqrMagnitude > 0.001f)
        throw new InvalidOperationException(
          $"Runtime timeline {label} does not have the expected position {expectedPosition.Value}."
        );
    }

    private static void ValidateRuntimeInstantiation(GameObject prefab)
    {
      GameObject canvasRoot = new GameObject("ReplayTimelineValidationCanvas", typeof(RectTransform));
      GameObject instance = null;
      try
      {
        RectTransform canvasRect = canvasRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1920f, 1080f);
        instance = UnityEngine.Object.Instantiate(prefab, canvasRect, false);

        UIFloatingPanelDragHandle dragHandle = instance.GetComponentInChildren<UIFloatingPanelDragHandle>(true);
        UIFloatingPanelDockController dockController = instance.GetComponent<UIFloatingPanelDockController>();
        if (dragHandle == null || dockController == null)
          throw new InvalidOperationException("Runtime timeline interaction components are invalid.");

        dragHandle.ClampPosition(Vector2.zero);
        dockController.ResetExpanded(default, false);
        dockController.Dock();
      }
      finally
      {
        if (instance != null)
          UnityEngine.Object.DestroyImmediate(instance);
        UnityEngine.Object.DestroyImmediate(canvasRoot);
      }
    }

    private static void ValidateTextFont(TMP_Text text, string label)
    {
      if (
        text == null
        || text.font == null
        || !string.Equals(
          AssetDatabase.GetAssetPath(text.font),
          TimelineTmpFontPath,
          StringComparison.OrdinalIgnoreCase
        )
      )
        throw new InvalidOperationException(
          "Runtime timeline " + label + " does not reference the MapleStory TMP font."
        );
    }

    private static void BuildBundle(BuildTarget target, string platformFolder)
    {
      string projectRoot = Directory.GetParent(Application.dataPath).FullName;
      string repositoryRoot = Directory.GetParent(projectRoot).FullName;
      string intermediate = Path.Combine(projectRoot, "Library", "TUFReplayAssetBundles", platformFolder);
      string destinationFolder = Path.Combine(repositoryRoot, "TUFReplay", "Assets", platformFolder);
      Directory.CreateDirectory(intermediate);
      Directory.CreateDirectory(destinationFolder);

      var build = new AssetBundleBuild { assetBundleName = BundleName, assetNames = new[] { RuntimePrefabPath } };
      AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
        intermediate,
        new[] { build },
        BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle,
        target
      );
      if (manifest == null)
        throw new InvalidOperationException("AssetBundle build failed for " + target + ".");

      string builtBundle = Path.Combine(intermediate, BundleName);
      string destination = Path.Combine(destinationFolder, BundleName);
      if (!File.Exists(builtBundle))
        throw new FileNotFoundException("Built bundle is missing.", builtBundle);
      File.Copy(builtBundle, destination, true);
    }
  }
}
