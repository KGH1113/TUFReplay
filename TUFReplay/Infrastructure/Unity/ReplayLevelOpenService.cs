using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TUFReplay.Infrastructure.Unity;

public static class ReplayLevelOpenService
{
  private const float VerifiedLevelHoldSeconds = 15f;
  private static string _heldLevelPath;
  private static scrUIController _heldUiController;
  private static double _holdExpiresAt;
  private static long _holdGeneration;

  public static void WipeForVerification(Action<scrUIController> onBlack, Action onCancelled)
  {
    if (onBlack == null)
      throw new ArgumentNullException(nameof(onBlack));

    scrUIController uiController = scrUIController.instance;
    if (uiController == null)
    {
      onBlack(null);
      return;
    }

    bool reachedBlack = false;
    uiController.WipeToBlack(
      WipeDirection.StartsFromRight,
      () =>
      {
        reachedBlack = true;
        onBlack(uiController);
      },
      () =>
      {
        // DOTween invokes OnKill after a normally completed auto-kill too.
        if (!reachedBlack)
          onCancelled?.Invoke();
      }
    );
  }

  public static void ReturnFromVerification(scrUIController uiController)
  {
    uiController?.WipeFromBlack();
  }

  public static void HoldVerifiedLevel(string levelPath, scrUIController uiController)
  {
    if (uiController == null)
      return;

    string canonicalPath = LevelPathIdentity.Canonicalize(levelPath);
    if (canonicalPath == null)
      return;

    ClearHeldState();
    _heldLevelPath = canonicalPath;
    _heldUiController = uiController;
    _holdExpiresAt = Time.realtimeSinceStartupAsDouble + VerifiedLevelHoldSeconds;
    _holdGeneration++;
  }

  public static void ReleaseHeldBlack()
  {
    long generation = _holdGeneration;
    UnityMainThread.Post(() => ReleaseHeldBlackOnMainThread(generation));
  }

  public static void Tick()
  {
    if (_heldLevelPath != null && Time.realtimeSinceStartupAsDouble >= _holdExpiresAt)
      ReleaseHeldBlackOnMainThread(_holdGeneration);
  }

  public static void OpenEditor(string levelPath)
  {
    if (string.IsNullOrWhiteSpace(levelPath))
      throw new ArgumentException("Level path is required.", nameof(levelPath));

    void LoadEditor()
    {
      GCS.sceneToLoad = "scnEditor";
      GCS.worldEntrance = null;
      scnEditor.levelToOpenOnLoad = levelPath;
      SceneManager.LoadScene("scnEditor");
    }

    if (ConsumeHeldLevel(levelPath))
      LoadEditor();
    else if (scrUIController.instance == null)
      LoadEditor();
    else
      scrUIController.instance.WipeToBlack(WipeDirection.StartsFromRight, LoadEditor, null);
  }

  private static bool ConsumeHeldLevel(string levelPath)
  {
    string canonicalPath = LevelPathIdentity.Canonicalize(levelPath);
    if (_heldLevelPath == null || !LevelPathIdentity.Equals(_heldLevelPath, canonicalPath))
      return false;

    ClearHeldState();
    return true;
  }

  private static void ReleaseHeldBlackOnMainThread(long generation)
  {
    if (_heldLevelPath == null || generation != _holdGeneration)
      return;

    scrUIController uiController = _heldUiController;
    ClearHeldState();
    uiController?.WipeFromBlack();
  }

  private static void ClearHeldState()
  {
    _heldLevelPath = null;
    _heldUiController = null;
    _holdExpiresAt = 0d;
  }
}
