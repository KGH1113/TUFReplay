using System;
using JALib.Tools;

namespace TUFReplay.Infrastructure.Unity;

public static class UnityMainThread
{
  public static void Initialize()
  {
    MainThread.WaitForMainThread().CatchException(Main.Instance);
  }

  public static void Post(Action action)
  {
    if (action == null) return;
    MainThread.Run(Main.Instance, action);
  }
}
