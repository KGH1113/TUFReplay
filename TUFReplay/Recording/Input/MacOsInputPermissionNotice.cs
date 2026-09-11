using UnityEngine;

namespace TUFReplay.Recording.Input;

internal sealed class MacOsInputPermissionNotice : MonoBehaviour
{
  private const string SettingsUrl = "x-apple.systempreferences:com.apple.preference.security?Privacy_ListenEvent";
  private static bool _shown;
  private static MacOsInputPermissionNotice _instance;
  private string _reason;
  private Rect _window = new Rect(0, 0, 540, 230);

  public static void Show(string reason)
  {
    if (_shown)
      return;
    _shown = true;
    GameObject gameObject = new GameObject("TUFReplay macOS Input Monitoring Notice");
    DontDestroyOnLoad(gameObject);
    _instance = gameObject.AddComponent<MacOsInputPermissionNotice>();
    _instance._reason = reason;
  }

  private void OnGUI()
  {
    _window.x = (Screen.width - _window.width) * 0.5f;
    _window.y = (Screen.height - _window.height) * 0.5f;
    _window = GUI.Window(GetInstanceID(), _window, DrawWindow, "TUFReplay Input Monitoring");
  }

  private void DrawWindow(int windowId)
  {
    GUILayout.Space(8);
    if (_reason != null && _reason.StartsWith("dylib_missing_or_invalid"))
    {
      GUILayout.Label("The macOS native input component is missing or invalid.");
      GUILayout.Label("Reinstall TUFReplay before recording keyboard input.");
    }
    else
    {
      GUILayout.Label("High-resolution keyboard recording needs macOS Input Monitoring permission.");
      GUILayout.Label("Allow A Dance of Fire and Ice in Privacy & Security > Input Monitoring, then restart the game.");
    }
    GUILayout.Space(8);
    GUILayout.Label("Reason: " + _reason);
    GUILayout.FlexibleSpace();
    GUILayout.BeginHorizontal();
    if (_reason == null || !_reason.StartsWith("dylib_missing_or_invalid"))
    {
      if (GUILayout.Button("Open System Settings", GUILayout.Height(32)))
        Application.OpenURL(SettingsUrl);
    }
    if (GUILayout.Button("Close", GUILayout.Height(32)))
    {
      Destroy(gameObject);
      _instance = null;
    }
    GUILayout.EndHorizontal();
    GUI.DragWindow(new Rect(0, 0, _window.width, 28));
  }
}
