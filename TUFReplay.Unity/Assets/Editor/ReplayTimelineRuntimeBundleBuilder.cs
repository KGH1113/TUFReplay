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

      RectTransform panel = FindRect(prefab.transform, "FloatingPanel");
      ValidateRect(panel, "floating panel", new Vector2(520f, 112f), null);
      ValidateRect(FindRect(panel, "PanelShadow"), "panel shadow", new Vector2(520f, 112f), null);
      ValidateRect(FindRect(panel, "LinearTimeline"), "timeline track", new Vector2(344f, 14f), null);
      ValidateRect(FindRect(panel, "PlayButton"), "play button", new Vector2(52f, 52f), new Vector2(0f, -18f));
      ValidateRect(FindRect(panel, "DockButton"), "dock button", new Vector2(44f, 40f), new Vector2(226f, -18f));
      ValidateRect(FindRect(prefab.transform, "DockTab"), "dock tab", new Vector2(44f, 48f), null);
      ValidateTextFont(FindRect(panel, "ElapsedTime")?.GetComponent<TMP_Text>(), "elapsed time");
      ValidateTextFont(FindRect(panel, "DurationTime")?.GetComponent<TMP_Text>(), "duration time");
      ValidateTextFont(FindRect(panel, "SeekBackward")?.Find("Label")?.GetComponent<TMP_Text>(), "backward seek label");
      ValidateTextFont(FindRect(panel, "SeekForward")?.Find("Label")?.GetComponent<TMP_Text>(), "forward seek label");
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
