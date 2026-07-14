using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TUFReplay.Infrastructure.Unity;

public static class ReplayLevelOpenService
{
  public static void OpenEditor(string levelPath)
  {
    if (string.IsNullOrWhiteSpace(levelPath)) throw new ArgumentException("Level path is required.", nameof(levelPath));

    void LoadEditor()
    {
      GCS.sceneToLoad = "scnEditor";
      GCS.worldEntrance = null;
      scnEditor.levelToOpenOnLoad = levelPath;
      SceneManager.LoadScene("scnEditor");
    }

    if (scrUIController.instance == null)
      LoadEditor();
    else
      scrUIController.instance.WipeToBlack(WipeDirection.StartsFromRight, LoadEditor, null);
  }
}
