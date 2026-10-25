using System;
using System.Collections.Generic;
using System.Threading;

namespace TUFReplay.Shared.Threading;

// Holds heavy background work while ADOFAI is judging input (countdown, checkpoint,
// player control). SkyHook timestamps async input on a Mono-attached native thread,
// so GC pauses and CPU contention from optional work shift judgments. Deferred work
// runs on the thread pool once judging stops; nothing here waits on the game thread.
public static class GameplayIdleGate
{
  private static readonly object Gate = new object();
  private static readonly List<Action> Deferred = new List<Action>();
  private static volatile bool _judging;

  public static event Action<Exception> WorkFailed;

  public static bool IsJudging => _judging;

  // Runs the work on the thread pool now, or after judging stops. Work that becomes
  // runnable while a new attempt has already started is deferred again.
  public static void Run(Action work)
  {
    if (work == null)
      throw new ArgumentNullException(nameof(work));
    lock (Gate)
    {
      if (_judging)
      {
        Deferred.Add(work);
        return;
      }
    }
    Dispatch(work);
  }

  // Called by the game-state monitor on the Unity thread.
  public static void SetJudging(bool judging)
  {
    Action[] ready;
    lock (Gate)
    {
      if (_judging == judging)
        return;
      _judging = judging;
      if (judging || Deferred.Count == 0)
        return;
      ready = Deferred.ToArray();
      Deferred.Clear();
    }
    foreach (Action work in ready)
      Dispatch(work);
  }

  // Lets deferred work proceed when the mod stops observing gameplay.
  public static void Release() => SetJudging(false);

  internal static int DeferredCount
  {
    get
    {
      lock (Gate)
        return Deferred.Count;
    }
  }

  private static void Dispatch(Action work)
  {
    ThreadPool.QueueUserWorkItem(_ =>
    {
      lock (Gate)
      {
        if (_judging)
        {
          Deferred.Add(work);
          return;
        }
      }
      try
      {
        work();
      }
      catch (Exception exception)
      {
        WorkFailed?.Invoke(exception);
      }
    });
  }
}
