# TUFReplay.Native.Windows

`TUFReplayNative.dll` holds the Windows work that must not run managed code.

- **Keyboard capture.** A dedicated native thread installs the `WH_KEYBOARD_LL` hook at above-normal priority. The hook procedure stamps `QueryPerformanceCounter`, copies the raw `KBDLLHOOKSTRUCT` fields into a lock-free single-producer ring, and returns. It never calls managed code, takes a lock, or waits for the consumer. Every low-level hook in the system runs in one serialized chain shared with ADOFAI's async input (SkyHook). A managed hook procedure would be stopped by every Mono GC pause and could be silently removed by Windows after `LowLevelHooksTimeout`. A managed bridge thread in `WindowsNativeKeyboardEventSource` drains the ring, converts each stamp with `WindowsPerformanceCounterConverter`, and applies the same key normalization as the managed fallback hook. The stamp is raw QPC, not `Stopwatch` ticks: Unity's Mono counts `Stopwatch` from its first clock read, so an unconverted stamp lands ahead by the system uptime at game launch.
- **Camera relay.** `tufreplay_camera.h` reads the FFmpeg DirectShow capture process's raw I420 output and `showinfo` timestamps on native threads, keeps a fixed frame pool, converts the live preview into the shared preview mapping, and streams each run's frames as Matroska into the encoder's input. A run's stream is prepared before the run and first sends a black warm-up frame, so the encoder opens its session while the level is open rather than at the first captured frame. The managed `FfmpegCameraSession` and `FfmpegCameraRecording` only manage processes and read counters, so camera frames add no Mono allocations or GC pauses to the game process. The core is platform-independent; `windows-native` also builds it as a host library for the C# end-to-end tests.
- **Audio endpoints.** `tufreplay_win_audio_capture_transport` follows an endpoint's device topology to its adapter and reports Bluetooth (`BTHENUM`, `BTHHFENUM`, `BTHLE`) capture devices.

## Build

```bash
./scripts/run.sh windows-native
```

The command cross-compiles the DLL for x86-64 Windows with zig on macOS or Linux, runs the platform-independent ring and camera relay tests on the build host, and validates exports and dependencies. The DLL only depends on system libraries and the Universal CRT. `mod-check` builds it when zig is installed; packaging requires it.

## Desktop smoke test

The build also produces `tufreplay-native-smoke.exe`. Run it in an interactive Windows session next to the DLL:

```bat
tufreplay-native-smoke.exe TUFReplayNative.dll
```

It installs the hook, injects 200 `F24` transitions with `SendInput`, and reports how long the hook took to receive them. It also reports the default recording endpoint's transport.
