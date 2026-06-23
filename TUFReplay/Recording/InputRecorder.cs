using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using ThreadPriority = System.Threading.ThreadPriority;

namespace TUFReplay.Recording;

public static class InputRecorder
{
  private static readonly KeyCode[] KeyCodes = CreateKeyCodes();
  private static readonly bool[] PreviousKeyState = new bool[KeyCodes.Length];
  private static readonly Stopwatch Stopwatch = new Stopwatch();
  private static Thread KeyInputListener;
  private static RecordingSession Session;
  private static volatile bool Running;

  public static void Start(RecordingSession session)
  {
    Stop();

    Session = session;
    Reset();
    Running = true;
    Stopwatch.Restart();
    KeyInputListener = new Thread(ListenKey)
    {
      Name = "TUFReplay Recording Input Listener Thread",
      Priority = ThreadPriority.AboveNormal,
      IsBackground = true
    };
    KeyInputListener.Start();
  }

  public static void Stop()
  {
    Running = false;

    if (KeyInputListener == null) return;

    try
    {
      KeyInputListener.Interrupt();
      if (!KeyInputListener.Join(200)) KeyInputListener.Abort();
    }
    catch
    {
      // Listener shutdown should not break mod unload.
    }

    KeyInputListener = null;
    Session = null;
  }

  public static void Reset()
  {
    for (int i = 0; i < PreviousKeyState.Length; i++) PreviousKeyState[i] = false;
  }

  private static void ListenKey()
  {
    long lastUpdateMillis = -1;

    try
    {
      while (Running)
      {
        long currentMillis = Stopwatch.ElapsedMilliseconds;
        while (Running && currentMillis == lastUpdateMillis)
        {
          Thread.Yield();
          currentMillis = Stopwatch.ElapsedMilliseconds;
        }

        lastUpdateMillis = currentMillis;
        Work();
      }
    }
    catch (ThreadAbortException)
    {
    }
    catch (ThreadInterruptedException)
    {
    }
    catch (Exception e)
    {
      Main.Instance?.LogException(e);
    }
  }

  private static void Work()
  {
    RecordingSession session = Session;
    if (session == null || !session.IsRecording) return;

    for (int i = 0; i < KeyCodes.Length; i++)
    {
      bool pressed = CheckKey(KeyCodes[i]);
      if (pressed == PreviousKeyState[i]) continue;

      PreviousKeyState[i] = pressed;
      session.AddInput((int)KeyCodes[i], pressed, GetSongPosition());
    }
  }

  private static bool CheckKey(KeyCode keyCode)
  {
    return Input.GetKey(keyCode);
  }

  private static double GetSongPosition()
  {
    return ADOBase.conductor != null ? ADOBase.conductor.songposition_minusi : 0d;
  }

  private static KeyCode[] CreateKeyCodes()
  {
    List<KeyCode> keyCodes = new List<KeyCode>();

    foreach (KeyCode keyCode in Enum.GetValues(typeof(KeyCode)))
    {
      if (keyCode > KeyCode.Mouse6) break;
      if (keyCode == KeyCode.None || keyCode == KeyCode.WheelUp || keyCode == KeyCode.WheelDown) continue;

      keyCodes.Add(keyCode);
    }

    return keyCodes.ToArray();
  }
}
