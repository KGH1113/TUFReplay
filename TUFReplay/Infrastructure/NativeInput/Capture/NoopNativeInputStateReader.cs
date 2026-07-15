using System.Collections.Generic;

namespace TUFReplay.Infrastructure.NativeInput.Capture;

internal sealed class NoopNativeInputStateReader : INativeInputStateReader
{
  private static readonly int[] NoKeyCodes = new int[0];

  public string Name => "unsupported";
  public IReadOnlyList<int> KeyCodes => NoKeyCodes;

  public void Refresh() { }

  public bool TryGetIsDown(int keyCode, out bool isDown)
  {
    isDown = false;
    return false;
  }
}
