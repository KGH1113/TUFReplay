using HarmonyLib;
using UnityEngine;

namespace TUFReplay.Shared.Game;

/// <summary>
/// The editor reads the keyboard when a play starts: Ctrl+Space plays at the shortcut speed and
/// Space at 1x. The music pitch and the "(0.75x)" after the level name both follow that speed.
/// A play started later, like a replay or a play resumed after a dialog, sets its speed here
/// instead of taking whatever keys are held at that moment.
/// </summary>
[HarmonyPatch]
internal static class EditorPlaySpeed
{
  private static float? _pending;

  /// <summary>The speed the editor would give a play started now.</summary>
  public static float FromHeldKeys() => RDInput.holdingControl ? Persistence.shortcutPlaySpeed / 100f : 1f;

  /// <summary>Starts an editor play at <paramref name="speed"/>, whatever keys are held.</summary>
  public static void Play(scnEditor editor, float speed)
  {
    _pending = speed;
    try
    {
      editor.Play();
    }
    finally
    {
      _pending = null;
    }
  }

  /// <summary>Sets the speed the music and the level name read next, as Ctrl+Space would.</summary>
  public static void Apply(scnEditor editor, float speed)
  {
    if (editor == null)
      return;
    editor.playbackSpeed = speed;
    GCS.editorQuickPitchedPlaying = !Mathf.Approximately(speed, 1f);
  }

  // editor.Play sets the speed from the keys, then sets up the music and later writes the level
  // name. Setting the speed when the music is set up covers both.
  [HarmonyPatch(typeof(scrConductor), nameof(scrConductor.SetupConductorWithLevelData))]
  [HarmonyPrefix]
  private static void OnSetupConductorPrefix()
  {
    if (_pending.HasValue)
      Apply(ADOBase.editor, _pending.Value);
  }
}
