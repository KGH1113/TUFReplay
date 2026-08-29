using System;
using System.Collections.Generic;

namespace TUFReplay.Recording.Input;

internal interface INativeInputEventSource
{
  string Name { get; }
  bool IsRunning { get; }
  IReadOnlyList<int> SnapshotKeyCodes { get; }
  void Start(Action<NativeInputTransition> onTransition);
  void Stop();
  bool TryGetPhysicalKeyState(int keyCode, out bool isDown);
}
