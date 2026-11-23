using UnityEngine;
using UnityEngine.UI;

namespace TUFReplay.Activity.Icons;

/// <summary>Finds ADOFAI's own run-flag sprites, so TUFReplay does not ship the game's art.</summary>
internal static class GameIconSprites
{
  public const string LenientBullseye = "lenientBullseye";
  public const string NormalBullseye = "normalBullseye";
  public const string StrictBullseye = "strictBullseye";
  public const string NoFail = "noFail";

  public static bool IsKnown(string icon) => icon is LenientBullseye or NormalBullseye or StrictBullseye or NoFail;

  /// <summary>Null when this ADOFAI build has no such sprite or the scene holding it is not loaded.</summary>
  public static Sprite Find(string icon) =>
    icon switch
    {
      LenientBullseye => Bullseye(Difficulty.Lenient),
      NormalBullseye => Bullseye(Difficulty.Normal),
      StrictBullseye => Bullseye(Difficulty.Strict),
      NoFail => NoFailBadge(),
      _ => null,
    };

  // ADOFAI shows the same sprites for the judgment difficulty in game and in the editor.
  private static Sprite Bullseye(Difficulty difficulty)
  {
    Sprite[] sprites = RDConstants.data != null ? RDConstants.data.bullseyeSprites : null;
    int index = (int)difficulty;
    return sprites != null && index < sprites.Length ? sprites[index] : null;
  }

  // The No-Fail badge belongs to the in-game UI, which the menu, gameplay and editor scenes load.
  private static Sprite NoFailBadge()
  {
    scrUIController controller = scrUIController.instance;
    Image image = controller != null ? controller.noFailImage : null;
    return image != null ? image.sprite : null;
  }
}
