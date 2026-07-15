using System;
using System.Collections.Concurrent;
using System.Threading;

namespace TUFReplay.Infrastructure.Unity;

public static class UnityMainThread
{
  private static readonly ConcurrentQueue<Action> Pending = new ConcurrentQueue<Action>();
  private static readonly SendOrPostCallback InvokePosted = state => Invoke((Action)state);

  private static SynchronizationContext _context;
  private static Thread _thread;

  public static void Initialize()
  {
    _thread = Thread.CurrentThread;
    SynchronizationContext context = SynchronizationContext.Current;
    _context = context?.GetType() == typeof(SynchronizationContext) ? null : context;
  }

  public static void Post(Action action)
  {
    if (action == null)
      return;

    if (Thread.CurrentThread == _thread)
    {
      Invoke(action);
      return;
    }

    if (_context != null)
    {
      _context.Post(InvokePosted, action);
      return;
    }

    Pending.Enqueue(action);
  }

  public static void DrainPending()
  {
    if (_context != null || Pending.IsEmpty)
      return;

    while (Pending.TryDequeue(out Action action))
      Invoke(action);
  }

  public static void Shutdown()
  {
    while (Pending.TryDequeue(out _)) { }
    _context = null;
    _thread = null;
  }

  private static void Invoke(Action action)
  {
    try
    {
      action();
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(UnityMainThread), exception);
    }
  }
}
