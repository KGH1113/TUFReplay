using System;

namespace TUFReplay.Recording.Input;

internal interface INativeInputEventSource
{
  string Name { get; }
  bool IsRunning { get; }
  void Start(Action<NativeInputTransition> onTransition);
  void Stop();
}
