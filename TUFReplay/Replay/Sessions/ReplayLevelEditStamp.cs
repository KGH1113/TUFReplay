using System;
using HarmonyLib;

namespace TUFReplay.Replay.Sessions;

// Changes whenever ADOFAI's editor records an edit. Every edit goes through
// scnEditor.SaveState, which stamps saveStateLastFrame and grows undoStates; undo
// and redo move states between the two lists.
internal static class ReplayLevelEditStamp
{
  public const long Unknown = long.MinValue;
  public const long NoEditor = -1L;

  private static readonly AccessTools.FieldRef<scnEditor, int> SaveStateLastFrame = CreateSaveStateAccessor();

  public static long Read(scnEditor editor)
  {
    if (editor == null)
      return NoEditor;
    if (SaveStateLastFrame == null)
      return Unknown;
    try
    {
      int frame = SaveStateLastFrame(editor);
      int undo = editor.undoStates?.Count ?? 0;
      int redo = editor.redoStates?.Count ?? 0;
      return ((long)frame << 16) ^ ((long)undo << 8) ^ redo;
    }
    catch (Exception)
    {
      return Unknown;
    }
  }

  private static AccessTools.FieldRef<scnEditor, int> CreateSaveStateAccessor()
  {
    try
    {
      return AccessTools.FieldRefAccess<scnEditor, int>("saveStateLastFrame");
    }
    catch (Exception)
    {
      // Without the stamp, every check hashes the level again, as before.
      return null;
    }
  }
}
