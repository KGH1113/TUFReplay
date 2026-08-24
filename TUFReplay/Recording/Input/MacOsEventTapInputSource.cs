using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace TUFReplay.Recording.Input;

internal sealed class MacOsEventTapInputSource : INativeInputEventSource
{
  private const int EventTapSession = 1;
  private const int EventTapTailAppend = 1;
  private const int EventTapListenOnly = 1;
  private const uint KeyDown = 10;
  private const uint KeyUp = 11;
  private const uint FlagsChanged = 12;
  private const uint TapDisabledByTimeout = 0xFFFFFFFE;
  private const uint TapDisabledByUserInput = 0xFFFFFFFF;
  private const int KeyboardEventKeycode = 9;
  private const uint Utf8Encoding = 0x08000100;

  private delegate IntPtr EventTapCallback(IntPtr proxy, uint type, IntPtr inputEvent, IntPtr userInfo);

  private readonly ManualResetEvent _started = new ManualResetEvent(false);
  private Action<NativeInputTransition> _onTransition;
  private Thread _thread;
  private EventTapCallback _callback;
  private IntPtr _tap;
  private IntPtr _runLoop;
  private volatile bool _running;
  private Exception _startFailure;
  private readonly bool[] _keyStates = new bool[128];

  public string Name => "macos-cgeventtap";
  public bool IsRunning => _running && _tap != IntPtr.Zero;

  public void Start(Action<NativeInputTransition> onTransition)
  {
    if (onTransition == null)
      throw new ArgumentNullException(nameof(onTransition));
    if (_thread != null)
      return;

    _onTransition = onTransition;
    _startFailure = null;
    _started.Reset();
    _thread = new Thread(Run)
    {
      IsBackground = true,
      Name = "TUFReplay macOS Keyboard Tap",
    };
    _thread.Start();
    if (!_started.WaitOne(2000) || !IsRunning)
    {
      Exception failure = _startFailure ?? new InvalidOperationException("macOS event tap startup timed out.");
      Stop();
      throw failure;
    }
  }

  public void Stop()
  {
    _running = false;
    IntPtr runLoop = _runLoop;
    if (runLoop != IntPtr.Zero)
      CFRunLoopStop(runLoop);
    if (_thread != null && Thread.CurrentThread != _thread)
      _thread.Join(2000);
    _thread = null;
    _runLoop = IntPtr.Zero;
    _onTransition = null;
  }

  private void Run()
  {
    IntPtr source = IntPtr.Zero;
    IntPtr mode = IntPtr.Zero;
    try
    {
      _callback = OnEvent;
      ulong mask = (1UL << (int)KeyDown) | (1UL << (int)KeyUp) | (1UL << (int)FlagsChanged);
      _tap = CGEventTapCreate(
        EventTapSession,
        EventTapTailAppend,
        EventTapListenOnly,
        mask,
        _callback,
        IntPtr.Zero
      );
      if (_tap == IntPtr.Zero)
        throw new InvalidOperationException("CGEventTapCreate failed; Input Monitoring permission may be missing.");

      source = CFMachPortCreateRunLoopSource(IntPtr.Zero, _tap, 0);
      if (source == IntPtr.Zero)
        throw new InvalidOperationException("CFMachPortCreateRunLoopSource failed.");
      mode = CFStringCreateWithCString(IntPtr.Zero, "kCFRunLoopDefaultMode", Utf8Encoding);
      _runLoop = CFRunLoopGetCurrent();
      CFRunLoopAddSource(_runLoop, source, mode);
      // macOS reports modifier transitions as flags-changed events. Seed only
      // the modifier virtual-key range so a key held before capture starts is
      // released correctly, without scanning every key during tap startup.
      for (int keyCode = 0x36; keyCode <= 0x3F; keyCode++)
        _keyStates[keyCode] = CGEventSourceKeyState(0, (ushort)keyCode);
      CGEventTapEnable(_tap, true);
      _running = true;
      _started.Set();
      CFRunLoopRun();
    }
    catch (Exception exception)
    {
      _startFailure = exception;
      _started.Set();
    }
    finally
    {
      _running = false;
      if (source != IntPtr.Zero)
        CFRelease(source);
      if (mode != IntPtr.Zero)
        CFRelease(mode);
      if (_tap != IntPtr.Zero)
        CFRelease(_tap);
      _tap = IntPtr.Zero;
      _callback = null;
    }
  }

  private IntPtr OnEvent(IntPtr proxy, uint type, IntPtr inputEvent, IntPtr userInfo)
  {
    if (type == TapDisabledByTimeout || type == TapDisabledByUserInput)
    {
      // Do not immediately re-enable a tap that macOS disabled for taking too
      // long. Re-enabling it here can create a disable/re-enable loop on the
      // WindowServer input path. The Unity thread observes IsRunning=false and
      // performs the normal one-shot restart/fallback sequence instead.
      _running = false;
      IntPtr runLoop = _runLoop;
      if (runLoop != IntPtr.Zero)
        CFRunLoopStop(runLoop);
      return inputEvent;
    }
    if (!_running || inputEvent == IntPtr.Zero)
      return inputEvent;
    if (type != KeyDown && type != KeyUp && type != FlagsChanged)
      return inputEvent;

    int keyCode = unchecked((int)CGEventGetIntegerValueField(inputEvent, KeyboardEventKeycode));
    bool down;
    if (type == FlagsChanged)
    {
      // A flags-changed event is itself the physical transition. Tracking the
      // per-key state locally avoids a synchronous CGEventSourceKeyState call
      // from inside the event-tap callback and preserves left/right modifiers.
      if ((uint)keyCode >= (uint)_keyStates.Length)
        return inputEvent;
      down = !_keyStates[keyCode];
    }
    else
    {
      down = type == KeyDown;
    }
    if ((uint)keyCode < (uint)_keyStates.Length)
      _keyStates[keyCode] = down;
    _onTransition?.Invoke(
      new NativeInputTransition(
        Stopwatch.GetTimestamp(),
        unchecked((long)CGEventGetTimestamp(inputEvent)),
        keyCode,
        down,
        false,
        keyCode,
        CGEventGetFlags(inputEvent)
      )
    );
    return inputEvent;
  }

  [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
  private static extern IntPtr CGEventTapCreate(
    int tap,
    int place,
    int options,
    ulong eventsOfInterest,
    EventTapCallback callback,
    IntPtr userInfo
  );

  [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
  private static extern void CGEventTapEnable(IntPtr tap, bool enable);

  [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
  private static extern long CGEventGetIntegerValueField(IntPtr inputEvent, int field);

  [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
  private static extern ulong CGEventGetTimestamp(IntPtr inputEvent);

  [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
  private static extern ulong CGEventGetFlags(IntPtr inputEvent);

  [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
  [return: MarshalAs(UnmanagedType.I1)]
  private static extern bool CGEventSourceKeyState(int stateId, ushort keyCode);

  [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
  private static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr port, int order);

  [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
  private static extern IntPtr CFRunLoopGetCurrent();

  [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
  private static extern void CFRunLoopAddSource(IntPtr runLoop, IntPtr source, IntPtr mode);

  [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
  private static extern void CFRunLoopRun();

  [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
  private static extern void CFRunLoopStop(IntPtr runLoop);

  [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
  private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string value, uint encoding);

  [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
  private static extern void CFRelease(IntPtr value);
}
