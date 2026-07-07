using System.Collections.Generic;

namespace TUFReplay.Infrastructure.NativeInput.Capture;

internal interface INativeInputStateReader
{
  string Name { get; }
  IReadOnlyList<int> KeyCodes { get; }
  void Refresh();
  bool TryGetIsDown(int keyCode, out bool isDown);
}
