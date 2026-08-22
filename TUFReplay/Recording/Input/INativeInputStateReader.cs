using System.Collections.Generic;

namespace TUFReplay.Recording.Input;

internal interface INativeInputStateReader
{
  string Name { get; }
  IReadOnlyList<int> KeyCodes { get; }
  void Refresh();
  void RefreshPhysicalState();
  bool TryGetIsDown(int keyCode, out bool isDown);
}
