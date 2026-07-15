using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TUFReplay.Infrastructure.NativeInput;

internal sealed class WindowsNativeInputFocusGuard : StableNativeInputFocusGuard
{
  private const uint GetWindowOwner = 4;

  private readonly uint _processId = (uint)Process.GetCurrentProcess().Id;
  private IntPtr _targetWindow;
  private IntPtr _foregroundWindow;
  private bool _targetMinimized;

  protected override bool EvaluateForegroundTarget(out string reason)
  {
    IntPtr targetWindow = ResolveTargetWindow();
    _foregroundWindow = GetForegroundWindow();
    _targetMinimized = targetWindow != IntPtr.Zero && IsIconic(targetWindow);

    if (!UnityEngine.Application.isFocused)
    {
      reason = "application_not_focused";
      return false;
    }

    if (targetWindow == IntPtr.Zero)
    {
      reason = "target_window_missing";
      return false;
    }

    if (_foregroundWindow != targetWindow)
    {
      reason = "foreground_window_mismatch";
      return false;
    }

    if (_targetMinimized)
    {
      reason = "target_window_minimized";
      return false;
    }

    reason = null;
    return true;
  }

  public override string Describe()
  {
    return "unityFocused="
      + UnityEngine.Application.isFocused
      + ", foregroundHwnd=0x"
      + _foregroundWindow.ToInt64().ToString("X")
      + ", targetHwnd=0x"
      + _targetWindow.ToInt64().ToString("X")
      + ", targetMinimized="
      + _targetMinimized;
  }

  private IntPtr ResolveTargetWindow()
  {
    if (IsValidTargetWindow(_targetWindow))
      return _targetWindow;

    IntPtr previous = _targetWindow;
    _targetWindow = ResolveProcessMainWindow();
    if (_targetWindow != previous)
      InvalidateStability();

    return _targetWindow;
  }

  private IntPtr ResolveProcessMainWindow()
  {
    using (Process process = Process.GetCurrentProcess())
    {
      process.Refresh();
      if (IsValidTargetWindow(process.MainWindowHandle))
        return process.MainWindowHandle;
    }

    IntPtr largestWindow = IntPtr.Zero;
    long largestArea = -1L;

    EnumWindows(
      (window, _) =>
      {
        if (!IsValidTargetWindow(window) || !GetWindowRect(window, out Rect rect))
          return true;

        long width = Math.Max(0L, (long)rect.Right - rect.Left);
        long height = Math.Max(0L, (long)rect.Bottom - rect.Top);
        long area = width * height;
        if (area > largestArea)
        {
          largestArea = area;
          largestWindow = window;
        }

        return true;
      },
      IntPtr.Zero
    );

    return largestWindow;
  }

  private bool IsValidTargetWindow(IntPtr window)
  {
    if (window == IntPtr.Zero || !IsWindow(window) || !IsWindowVisible(window))
      return false;
    if (GetWindow(window, GetWindowOwner) != IntPtr.Zero)
      return false;

    GetWindowThreadProcessId(window, out uint processId);
    return processId == _processId;
  }

  private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

  [StructLayout(LayoutKind.Sequential)]
  private struct Rect
  {
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
  }

  [DllImport("user32.dll")]
  private static extern IntPtr GetForegroundWindow();

  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool IsIconic(IntPtr window);

  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool IsWindow(IntPtr window);

  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool IsWindowVisible(IntPtr window);

  [DllImport("user32.dll")]
  private static extern IntPtr GetWindow(IntPtr window, uint command);

  [DllImport("user32.dll")]
  private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool GetWindowRect(IntPtr window, out Rect rect);
}
