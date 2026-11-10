# Other APIs

Find these by name too, with no reference to TUFReplay.dll. Cache the resolved type and members. Treat a missing assembly, type or member as "no replay" and "not paused".

## ReplayRuntime

`TUFReplay.ReplayRuntime.IsPlaybackActive` (static `bool`) is true for the whole TUFReplay replay: preparation, level loading, playback, and the return to the editor. Read it at the lifecycle points you care about, not every frame. `ReplayRuntime.ApiVersion` is 1.

## NativeInputPause

Pause TUFReplay's native input while your own UI takes the keyboard away from the game, for example while your settings window is open.

```csharp
namespace TUFReplay
{
    public static class NativeInputPause
    {
        public const int ApiVersion = 1;
        public static bool IsPaused { get; }          // the UMM window or any owner pauses input
        public static void Request(string owner);     // your UI takes the keyboard
        public static void Release(string owner);     // it gives it back, and when your mod is disabled
    }
}
```

- While any owner holds a request, recording ignores native keyboard and mouse-button input and replays send none, the same as while the UnityModManager window is open. Input resumes 0.2 s after the last release.
- Requests are keyed by owner, so repeating one is harmless and one release ends it. Use a stable id such as `MyMod.Settings`. A null or blank owner throws `ArgumentException`.
- Both methods can be called from any thread.
- Request a pause only while ADOFAI itself does not get those keys. If TUFReplay stops recording input the game judges, the replay will not match the run.

```csharp
Type pause = AppDomain.CurrentDomain.GetAssemblies()
  .FirstOrDefault(assembly => assembly.GetName().Name == "TUFReplay")
  ?.GetType("TUFReplay.NativeInputPause");
var request = (Action<string>)Delegate.CreateDelegate(
  typeof(Action<string>),
  pause.GetMethod("Request", new[] { typeof(string) })
);
request("MyMod.Settings"); // resolve "Release" the same way
```
