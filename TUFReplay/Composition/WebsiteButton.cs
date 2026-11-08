using TUFReplay.Shared.Build;
using UnityEngine;

namespace TUFReplay.Composition;

// Opens the companion site from the Unity Mod Manager settings panel.
// Kept out of Main so loading Main never needs Unity's IMGUI module.
internal static class WebsiteButton
{
  private static readonly GUILayoutOption[] Layout = { GUILayout.ExpandWidth(false) };

  public static void Draw()
  {
    string label = RDString.language == SystemLanguage.Korean ? "TUFReplay 웹사이트 열기" : "Open TUFReplay website";
    if (GUILayout.Button(label, Layout))
      Application.OpenURL(TUFReplayBuildFlavor.WebsiteUrl);
  }
}
