using System;
using System.Collections.Generic;

namespace TUFReplay.Recording.Input;

internal interface INativeInputEventSource
{
  string Name { get; }
  bool IsRunning { get; }
  bool UsesExtendedKeyState { get; }
  IReadOnlyList<int> SnapshotKeyCodes { get; }
  void Start(Action<NativeInputTransition> onTransition);
  void Stop();
  void RefreshPhysicalState();
  bool TryGetPhysicalKeyState(int keyCode, out bool isDown);
  long ConsumeDroppedEvents();
  NativeInputSourceDiagnostics GetDiagnostics();
}
