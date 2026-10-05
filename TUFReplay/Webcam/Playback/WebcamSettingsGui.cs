using Newtonsoft.Json.Linq;
using TUFReplay.Composition;
using TUFReplay.Webcam.Models;
using UnityEngine;

namespace TUFReplay.Webcam.Playback;

public static class WebcamSettingsGui
{
  private static readonly JObject Pending = new JObject();
  private static string _error;
  private static TUFReplay.Webcam.Ipc.WebcamStateDto _layoutState;
  private static string _layoutError;

  public static void Draw()
  {
    var feature = FeatureRegistry.WebcamRecording;
    if (feature == null)
      return;
    // Use the same snapshot for Layout and its following input/repaint events.
    // Camera discovery and capture errors can change the number of controls.
    if (Event.current.type == EventType.Layout || _layoutState == null)
    {
      _layoutState = feature.GetState();
      _layoutError = _error ?? _layoutState.Error;
    }
    var state = _layoutState;
    GUILayout.Space(12);
    GUILayout.Label("Camera");
    if (!state.Supported)
    {
      GUILayout.Label("Camera recording is available on macOS and Windows.");
      return;
    }
    bool originalEnabled = GUI.enabled;
    GUI.enabled = originalEnabled && !state.CaptureLocked;
    bool enabled = GUILayout.Toggle(state.Enabled, "Camera on (stays active while the game is open)");
    if (enabled != state.Enabled)
      Commit(new JObject { ["enabled"] = enabled });
    if (state.Status == "recording" || state.Status == "saving")
      GUILayout.Label("Status: " + state.Status);
    GUILayout.Label("Video only. Microphone audio uses the existing microphone settings.");
    if (GUILayout.Button("Refresh cameras"))
      feature.RefreshDevices();
    var names = new string[state.Devices.Count + 1];
    names[0] = "System default camera";
    int selected = 0;
    for (int index = 0; index < state.Devices.Count; index++)
    {
      names[index + 1] = state.Devices[index].Name;
      if (state.Devices[index].Id == state.SelectedDeviceId)
        selected = index + 1;
    }
    int nextDevice = GUILayout.SelectionGrid(selected, names, 1);
    if (nextDevice != selected)
      Commit(new JObject { ["deviceId"] = nextDevice == 0 ? null : state.Devices[nextDevice - 1].Id });
    int quality =
      state.Quality == "quality" ? 2
      : state.Quality == "balanced" ? 1
      : 0;
    int nextQuality = GUILayout.SelectionGrid(
      quality,
      new[]
      {
        "Compact · 480p / 24 fps · about 4.5 MB/min",
        "Balanced · 720p / 30 fps · about 9 MB/min",
        "Quality · 1080p / 30 fps · about 22.5 MB/min",
      },
      1
    );
    if (quality != nextQuality)
      Commit(
        new JObject
        {
          ["quality"] =
            nextQuality == 2 ? "quality"
            : nextQuality == 1 ? "balanced"
            : "compact",
        }
      );
    Slider("storageLimitMb", "Total video storage (MB)", state.StorageLimitMb, 64, 8192, true);
    GUILayout.Label("Maximum space for all saved camera videos. When full, the oldest videos are removed first.");
    Slider("retentionDays", "Keep videos for (days)", state.RetentionDays, 1, 30, true);
    GUILayout.Label("Videos older than this are removed automatically. Runs and replays stay in your history.");
    GUILayout.Label("Older videos are removed first. Each recording is limited to 128 MB.");
    GUI.enabled = originalEnabled;
    bool liveVisible = GUILayout.Toggle(state.LiveVisible, "Show live camera while playing");
    if (liveVisible != state.LiveVisible)
      Commit(new JObject { ["liveVisible"] = liveVisible });
    bool visible = GUILayout.Toggle(state.PlaybackVisible, "Show camera video during replay");
    if (visible != state.PlaybackVisible)
      Commit(new JObject { ["playbackVisible"] = visible });
    bool mirror = GUILayout.Toggle(state.Mirror, "Mirror camera video");
    if (mirror != state.Mirror)
      Commit(new JObject { ["mirror"] = mirror });
    Slider("offsetMs", "Sync correction (ms)", state.OffsetMs, -1000, 1000, true);
    GUILayout.Label(
      "Positive values show the image earlier. Pause a replay near a key press and compare it with the camera image."
    );
    Slider("overlayWidth", "Width (fraction of game window)", state.OverlayWidth, 0.1f, 0.5f, false);
    GUILayout.Label(
      "Drag the camera in the game to move it. Drag any edge or corner to resize it. Changes are saved automatically."
    );
    if (GUILayout.Button("Reset camera position"))
      Commit(
        new JObject
        {
          ["overlayX"] = 1,
          ["overlayY"] = 1,
          ["overlayWidth"] = 0.22,
        }
      );
    if (Pending.HasValues && (Event.current.rawType == EventType.MouseUp || Event.current.rawType == EventType.KeyUp))
    {
      Commit((JObject)Pending.DeepClone());
      Pending.RemoveAll();
    }
    if (!string.IsNullOrEmpty(_layoutError))
      GUILayout.Label(_layoutError);
  }

  private static void Slider(string key, string label, double value, float min, float max, bool integer)
  {
    double current = Pending[key]?.Value<double>() ?? value;
    GUILayout.Label(label + ": " + current.ToString(integer ? "0" : "0.00"));
    double next = GUILayout.HorizontalSlider((float)current, min, max);
    if (integer)
      next = System.Math.Round(next);
    if (GUI.enabled && System.Math.Abs(next - current) > 0.00001)
      Pending[key] = integer ? new JValue((int)next) : new JValue(next);
  }

  private static void Commit(JObject values)
  {
    if (!WebcamSettingsPatch.TryParse(values, out var patch))
      return;
    _error = FeatureRegistry.WebcamRecording.TryUpdate(patch, out string message) ? null : message;
  }
}
