using System;
using System.Collections.Generic;
using System.IO;
using AdofaiIpc;
using TUFReplay.Activity.Icons;
using TUFReplay.Composition;
using TUFReplay.Shared.Ipc;
using TUFReplay.Shared.Ipc.Generated;
using TUFReplay.Shared.Threading;
using TUFReplay.Shared.Unity;
using UnityEngine;

namespace TUFReplay.Activity.Ipc;

/// <summary>Hands the companion ADOFAI's run-flag icons as PNGs copied from the running game.</summary>
internal sealed class GameIconIpc
{
  // Read on the game thread only, so the cache needs no lock.
  private readonly Dictionary<string, byte[]> _pngs = new Dictionary<string, byte[]>(StringComparer.Ordinal);

  public GameIcon Read(string icon)
  {
    if (!GameIconSprites.IsKnown(icon))
      throw IpcDomainError.Create("invalid_game_icon", "The requested game icon does not exist.");
    if (!_pngs.TryGetValue(icon, out byte[] png))
    {
      // Reading a texture back from the GPU can stall a frame, so it waits until the attempt ends.
      if (GameplayIdleGate.IsJudging)
        throw IpcDomainError.Create("game_icon_paused", "Game icons load after the current attempt.");
      png = Encode(icon);
      if (png == null)
        return null;
      _pngs[icon] = png;
    }
    return new GameIcon
    {
      File = IpcFile.FromStream(new MemoryStream(png, writable: false), png.Length, icon + ".png", "image/png"),
    };
  }

  private static byte[] Encode(string icon)
  {
    Sprite sprite = GameIconSprites.Find(icon);
    if (sprite == null)
      return null;
    try
    {
      return SpritePngEncoder.Encode(sprite);
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException("Activity/GameIcon", exception);
      return null;
    }
  }
}
