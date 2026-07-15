using System;

namespace TUFReplay.Infrastructure.NativeInput.Capture;

internal interface INativeInputEventSource
{
  string Name { get; }
  bool IsRunning { get; }
  void Start(Action<NativeInputTransition> onTransition);
  void Stop();
}
