using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using TUFReplay.Shared.NativeInput;

namespace TUFReplay.Recording.Input;

internal sealed class WindowsLowLevelKeyboardEventSource : INativeInputEventSource
{
  private const int WhKeyboardLl = 13;
  private const uint WmKeyDown = 0x0100;
  private const uint WmKeyUp = 0x0101;
  private const uint WmSysKeyDown = 0x0104;
  private const uint WmSysKeyUp = 0x0105;
  private const uint WmQuit = 0x0012;
  private const uint LlkhfExtended = 0x01;

  [StructLayout(LayoutKind.Sequential)]
  private struct KeyboardHookData
  {
    public uint VirtualKey;
    public uint ScanCode;
    public uint Flags;
    public uint Time;
    public UIntPtr ExtraInfo;
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct Message
  {
    public IntPtr Window;
    public uint Id;
    public UIntPtr WParam;
    public IntPtr LParam;
    public uint Time;
    public int PointX;
    public int PointY;
    public uint Private;
  }

  private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

  private readonly ManualResetEvent _started = new ManualResetEvent(false);
  private Action<NativeInputTransition> _onTransition;
  private Thread _thread;
  private HookProc _hookProc;
  private IntPtr _hook;
  private uint _threadId;
  private volatile bool _running;
  private Exception _startFailure;

  public string Name => "windows-wh-keyboard-ll";
  public bool IsRunning => _running && _hook != IntPtr.Zero;

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
      Name = "TUFReplay Windows Keyboard Hook",
    };
    _thread.Start();
    if (!_started.WaitOne(2000) || !IsRunning)
    {
      Exception failure = _startFailure ?? new InvalidOperationException("Windows keyboard hook startup timed out.");
      Stop();
      throw failure;
    }
  }

  public void Stop()
  {
    _running = false;
    uint threadId = _threadId;
    if (threadId != 0)
      PostThreadMessage(threadId, WmQuit, UIntPtr.Zero, IntPtr.Zero);
    if (_thread != null && Thread.CurrentThread != _thread)
      _thread.Join(2000);
    _thread = null;
    _threadId = 0;
    _onTransition = null;
  }

  private void Run()
  {
    try
    {
      _threadId = GetCurrentThreadId();
      PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
      _hookProc = OnHook;
      _hook = SetWindowsHookEx(WhKeyboardLl, _hookProc, GetModuleHandle(null), 0);
      if (_hook == IntPtr.Zero)
        throw new InvalidOperationException("SetWindowsHookEx failed. error=" + Marshal.GetLastWin32Error());
      _running = true;
      _started.Set();

      while (GetMessage(out Message message, IntPtr.Zero, 0, 0) > 0)
      {
        TranslateMessage(ref message);
        DispatchMessage(ref message);
      }
    }
    catch (Exception exception)
    {
      _startFailure = exception;
      _started.Set();
    }
    finally
    {
      _running = false;
      if (_hook != IntPtr.Zero)
        UnhookWindowsHookEx(_hook);
      _hook = IntPtr.Zero;
      _hookProc = null;
    }
  }

  private IntPtr OnHook(int code, IntPtr wParam, IntPtr lParam)
  {
    if (code >= 0 && _running)
    {
      uint message = unchecked((uint)wParam.ToInt64());
      bool down = message == WmKeyDown || message == WmSysKeyDown;
      bool up = message == WmKeyUp || message == WmSysKeyUp;
      if (down || up)
      {
        KeyboardHookData data = Marshal.PtrToStructure<KeyboardHookData>(lParam);
        bool extended = (data.Flags & LlkhfExtended) != 0;
        int key = NormalizeModifierKey(unchecked((int)data.VirtualKey), unchecked((int)data.ScanCode), extended);
        if (key > 0 && key <= byte.MaxValue && !WindowsNativeInputKey.IsMouseButton(key))
        {
          _onTransition?.Invoke(
            new NativeInputTransition(
              Stopwatch.GetTimestamp(),
              data.Time * 1_000_000L,
              key,
              down,
              extended,
              unchecked((int)data.ScanCode),
              data.Flags
            )
          );
        }
      }
    }
    return CallNextHookEx(_hook, code, wParam, lParam);
  }

  internal static int NormalizeModifierKey(int virtualKey, int scanCode, bool extended)
  {
    switch (virtualKey)
    {
      case 0x10: // VK_SHIFT
        return scanCode == 0x36 ? 0xA1 : 0xA0;
      case 0x11: // VK_CONTROL
        return extended ? 0xA3 : 0xA2;
      case 0x12: // VK_MENU
        return extended ? 0xA5 : 0xA4;
      default:
        return virtualKey;
    }
  }

  [DllImport("user32.dll", SetLastError = true)]
  private static extern IntPtr SetWindowsHookEx(int hook, HookProc callback, IntPtr module, uint threadId);

  [DllImport("user32.dll", SetLastError = true)]
  private static extern bool UnhookWindowsHookEx(IntPtr hook);

  [DllImport("user32.dll")]
  private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

  [DllImport("user32.dll", SetLastError = true)]
  private static extern int GetMessage(out Message message, IntPtr window, uint min, uint max);

  [DllImport("user32.dll")]
  private static extern bool PeekMessage(out Message message, IntPtr window, uint min, uint max, uint remove);

  [DllImport("user32.dll")]
  private static extern bool TranslateMessage(ref Message message);

  [DllImport("user32.dll")]
  private static extern IntPtr DispatchMessage(ref Message message);

  [DllImport("user32.dll", SetLastError = true)]
  private static extern bool PostThreadMessage(uint threadId, uint message, UIntPtr wParam, IntPtr lParam);

  [DllImport("kernel32.dll")]
  private static extern uint GetCurrentThreadId();

  [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
  private static extern IntPtr GetModuleHandle(string moduleName);
}
